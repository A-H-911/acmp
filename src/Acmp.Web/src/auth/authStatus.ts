/*
 * Transient auth-status flag carried across the Keycloak logout / expiry redirect.
 * Set before signoutRedirect (or on token expiry); consumed once by the LoginPage to
 * show "signed out" / "session expired". sessionStorage survives the round-trip to
 * Keycloak and back (same origin) and is cleared on read.
 */
const KEY = 'acmp:auth-status';
export type AuthStatus = 'signed_out' | 'session_expired';

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
    return v === 'signed_out' || v === 'session_expired' ? v : null;
  } catch {
    return null;
  }
}
