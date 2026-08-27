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

/**
 * Why the server refused an otherwise-valid token (ADR-0039). Sent as `X-Acmp-Auth-Reason` beside the
 * 401, and the distinction is load-bearing rather than informational:
 *  - `roles_changed` is fixed by getting a new token, which the SPA already does automatically;
 *  - `access_expired` and `account_disabled` are NOT retryable, and renewing against a session that no
 *    longer exists is a loop. AC-092 needs the page to SAY access has ended, not to spin.
 */
export type AuthRefusal = 'roles_changed' | 'access_expired' | 'account_disabled';

export class ApiError extends Error {
  readonly status: number;
  readonly problem?: ProblemDetails;
  /** Present only on a 401 the server explained. */
  readonly authRefusal?: AuthRefusal;
  constructor(status: number, problem?: ProblemDetails, authRefusal?: AuthRefusal) {
    super(problem?.title ?? `HTTP ${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
    this.authRefusal = authRefusal;
  }

  /** True when no new token can help — the account's access has ended (AC-092). */
  get isAccessEnded(): boolean {
    return this.authRefusal === 'access_expired' || this.authRefusal === 'account_disabled';
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
    const reason = res.headers.get('X-Acmp-Auth-Reason');
    throw new ApiError(res.status, problem, (reason as AuthRefusal | null) ?? undefined);
  }

  return res.status === 204 ? (undefined as T) : ((await res.json()) as T);
}

/**
 * A file download from an AUTHORIZED endpoint (WBS-24.6, the audit export).
 *
 * ⚠ WHY THIS EXISTS RATHER THAN AN `<a href>`. A plain link cannot carry the bearer token, so pointing
 * one at a protected route yields a 401 — and the browser renders that as a broken download rather than
 * as an error the app can show. The Reports page never hit this because its CSV is built client-side
 * from data already fetched; an audited server-side export cannot be.
 *
 * Shares `api`'s token getter and its ApiError projection deliberately: a 403 from the export must be
 * the same typed refusal every other call produces, not a special case at one call site.
 */
export async function apiBlob(path: string, init: RequestInit = {}): Promise<Blob> {
  const token = getToken();
  const res = await fetch(`/api${path}`, {
    ...init,
    headers: {
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
    const reason = res.headers.get('X-Acmp-Auth-Reason');
    throw new ApiError(res.status, problem, (reason as AuthRefusal | null) ?? undefined);
  }

  return res.blob();
}
