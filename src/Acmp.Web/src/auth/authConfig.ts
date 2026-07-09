/*
 * OIDC configuration for Keycloak (authorization-code + PKCE). All values come
 * from VITE_OIDC_* env vars — no secrets in source (guardrail 7). PKCE is on by
 * default in oidc-client-ts for response_type=code; no client secret in a
 * public SPA client.
 *
 * Tokens live in sessionStorage (oidc-client-ts default), not localStorage:
 * cleared on tab close and the smaller XSS exposure window. A strict CSP
 * (P16) is the complementary control. A cookie/BFF model is heavier than a
 * ≤20-user on-prem tool warrants (guardrail 12).
 */
import { WebStorageStateStore } from 'oidc-client-ts';
import type { AuthProviderProps } from 'react-oidc-context';

const authority = import.meta.env.VITE_OIDC_AUTHORITY as string | undefined;
const clientId = import.meta.env.VITE_OIDC_CLIENT_ID as string | undefined;
const scope = (import.meta.env.VITE_OIDC_SCOPE as string | undefined) ?? 'openid profile email';

/**
 * sessionStorage key that carries the post-login destination across the Keycloak round-trip.
 * A notification-card deep link opens a fresh (unauthenticated) tab; without this the user would
 * log in and land on `/` instead of the meeting/decision the card pointed at. `signIn(returnTo)`
 * sets it (or clears it — a plain sign-in must not inherit a stale path), `onSigninCallback` reads
 * it to fix the URL bar, and AuthCallbackPage consumes + removes it as the real navigation.
 */
export const RETURN_KEY = 'acmp-return-to';

/**
 * The deep-link path `signIn` should stash for post-login restore, or `null` when the key should be
 * cleared instead. Returns null for no path, `/`, and the auth routes themselves — so a plain sign-in
 * never inherits a stale target left by an abandoned deep-link login (the reason it clears, not just skips).
 */
export function returnPathToStore(returnTo?: string): string | null {
  if (!returnTo || returnTo === '/' || returnTo === '/login' || returnTo.startsWith('/auth')) return null;
  return returnTo;
}

/** True when a Keycloak realm is configured. When false, see AuthProvider. */
export const oidcEnabled = Boolean(authority && clientId);

export const oidcConfig: AuthProviderProps = {
  authority: authority ?? '',
  client_id: clientId ?? '',
  redirect_uri: `${window.location.origin}/auth/callback`,
  post_logout_redirect_uri: window.location.origin,
  response_type: 'code',
  scope,
  automaticSilentRenew: true,
  userStore: new WebStorageStateStore({ store: window.sessionStorage }),
  // Strip the OIDC code/state from the URL after a successful login, restoring the deep-link path the
  // user was heading to (default '/'). AuthCallbackPage performs the real route change + clears the key.
  onSigninCallback: () => {
    const to = sessionStorage.getItem(RETURN_KEY) ?? '/';
    window.history.replaceState({}, document.title, to);
  },
};
