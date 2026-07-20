/*
 * Thin fetch wrapper for the ACMP REST API. Attaches the bearer token and the
 * active locale, and turns the backend's RFC 7807 Problem Details into a typed
 * ApiError. The token getter is injected by the auth layer so this module stays
 * React-free. No endpoint hooks exist yet (P3 has no real data to fetch).
 */
import i18n from '../i18n';

// BL-016: one validation failure as the server actually emits it (GlobalExceptionHandler projects the
// FluentValidation PropertyName/ErrorMessage/ErrorCode). Previously typed as Record<string,string[]> — the
// ASP.NET ValidationProblemDetails shape — which did NOT match the wire and was never consumed.
export interface ProblemError {
  propertyName?: string;
  errorMessage?: string;
  errorCode?: string;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: ProblemError[];
}

/**
 * BL-016: localize a validation ProblemDetails (EN/AR). Maps the first failure's stable ErrorCode to an
 * `errors.<code>` i18n key; falls back to the server's English ErrorMessage, then to the generic message.
 */
export function localizedValidationMessage(problem?: ProblemDetails): string | undefined {
  const first = problem?.errors?.[0];
  if (!first) return problem?.title;
  if (first.errorCode) {
    const translated = i18n.t(`errors.${first.errorCode}`, { defaultValue: '' });
    if (translated) return translated;
  }
  return first.errorMessage ?? i18n.t('errors.generic', { defaultValue: problem?.title ?? '' });
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
