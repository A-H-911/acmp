/*
 * Thin fetch wrapper for the ACMP REST API. Attaches the bearer token and the
 * active locale, and turns the backend's RFC 7807 Problem Details into a typed
 * ApiError. The token getter is injected by the auth layer so this module stays
 * React-free. No endpoint hooks exist yet (P3 has no real data to fetch).
 */
import i18n from '../i18n';

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {
  readonly status: number;
  readonly problem?: ProblemDetails;
  constructor(status: number, problem?: ProblemDetails) {
    super(problem?.title ?? `HTTP ${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }
}

type TokenGetter = () => string | undefined;
let getToken: TokenGetter = () => undefined;

/** Wired once by the auth layer so requests carry the current access token. */
export function setTokenGetter(fn: TokenGetter): void {
  getToken = fn;
}

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = getToken();
  const res = await fetch(`/api${path}`, {
    ...init,
    headers: {
      Accept: 'application/json',
      'Accept-Language': i18n.language,
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...init.headers,
    },
  });

  if (!res.ok) {
    let problem: ProblemDetails | undefined;
    try {
      problem = (await res.json()) as ProblemDetails;
    } catch {
      // Non-JSON error body — fall back to the status code.
    }
    throw new ApiError(res.status, problem);
  }

  return res.status === 204 ? (undefined as T) : ((await res.json()) as T);
}
