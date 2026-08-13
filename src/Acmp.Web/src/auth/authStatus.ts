/*
 * Transient auth-status flag carried across the Keycloak logout / expiry redirect.
 * Set before signoutRedirect (or on token expiry); consumed once by the LoginPage to
 * show "signed out" / "session expired". sessionStorage survives the round-trip to
 * Keycloak and back (same origin) and is cleared on read.
 */
const KEY = 'acmp:auth-status';
// `idle_timeout` is distinct from `session_expired` on purpose (AC-004 / OQ-076): an expiry is
// something that happened TO the session, while this is a deliberate sign-out because nobody was
// there. Telling a returning user "you were signed out after 30 minutes of inactivity" explains a
// logout they did not ask for; "session expired" would read as a fault.
export type AuthStatus = 'signed_out' | 'session_expired' | 'idle_timeout';

export function setAuthStatus(status: AuthStatus): void {
  try {
    sessionStorage.setItem(KEY, status);
  } catch {
    /* storage unavailable — non-fatal */
  }
}

export function consumeAuthStatus(): AuthStatus | null {
  try {
    const v = sessionStorage.getItem(KEY);
    if (v) sessionStorage.removeItem(KEY);
    return v === 'signed_out' || v === 'session_expired' || v === 'idle_timeout' ? v : null;
  } catch {
    return null;
  }
}
