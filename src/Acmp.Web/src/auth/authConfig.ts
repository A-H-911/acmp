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
  // Strip the OIDC code/state from the URL after a successful login.
  onSigninCallback: () => {
    window.history.replaceState({}, document.title, '/');
  },
};
