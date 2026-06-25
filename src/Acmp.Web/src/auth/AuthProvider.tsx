/*
 * Picks the auth backend and exposes the unified AcmpAuth context:
 *   1. Keycloak configured  → real OIDC (react-oidc-context) in every build.
 *   2. Not configured + DEV → an in-memory stub with a role switcher, so the
 *      shell is runnable/testable without a live IdP. NEVER reachable in a
 *      production bundle (guarded by import.meta.env.DEV) — a stub that mints
 *      roles would be an auth bypass (guardrail 4).
 *   3. Not configured + PROD → fail closed (unauthenticated, error surfaced).
 *
 * Live Keycloak login + server claim→role mapping land in P4; here we wire the
 * flow and gate the UI. Nav/route gating hides UI; the API enforces access.
 */
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { AuthProvider as OidcProvider, useAuth as useOidc } from 'react-oidc-context';
import { AcmpAuthContext, type AcmpAuth } from './AcmpAuthContext';
import { oidcConfig, oidcEnabled } from './authConfig';
import { rolesFromClaims, type CommitteeRole } from './roles';
import { claimStringsFrom, displayNameFrom, initialsFrom, type OidcProfileLike } from './oidcProfile';
import { setTokenGetter } from '../api/apiClient';

function OidcBridge({ children }: { children: ReactNode }) {
  const oidc = useOidc();
  // Feed the current access token to the API client (outside React).
  useEffect(() => {
    setTokenGetter(() => oidc.user?.access_token);
  }, [oidc.user]);
  const value = useMemo<AcmpAuth>(() => {
    const profile = oidc.user?.profile as OidcProfileLike | undefined;
    const name = displayNameFrom(profile);
    return {
      isLoading: oidc.isLoading,
      isAuthenticated: oidc.isAuthenticated,
      error: oidc.error?.message,
      roles: rolesFromClaims(claimStringsFrom(profile)),
      displayName: name,
      initials: initialsFrom(name),
      signIn: () => void oidc.signinRedirect(),
      signOut: () => void oidc.signoutRedirect(),
    };
  }, [oidc]);
  return <AcmpAuthContext.Provider value={value}>{children}</AcmpAuthContext.Provider>;
}

const DEV_ROLE_KEY = 'acmp-dev-roles';

function DevAuthProvider({ children }: { children: ReactNode }) {
  const [roles, setRoles] = useState<CommitteeRole[]>(() => {
    const stored = sessionStorage.getItem(DEV_ROLE_KEY);
    return stored ? (JSON.parse(stored) as CommitteeRole[]) : ['secretary'];
  });
  const devSetRoles = useCallback((next: CommitteeRole[]) => {
    sessionStorage.setItem(DEV_ROLE_KEY, JSON.stringify(next));
    setRoles(next);
  }, []);
  const value = useMemo<AcmpAuth>(() => ({
    isLoading: false,
    isAuthenticated: true,
    roles,
    displayName: 'Dev User',
    initials: 'DV',
    signIn: () => {},
    signOut: () => {},
    devSetRoles,
  }), [roles, devSetRoles]);
  return <AcmpAuthContext.Provider value={value}>{children}</AcmpAuthContext.Provider>;
}

const FAIL_CLOSED: AcmpAuth = {
  isLoading: false,
  isAuthenticated: false,
  error: 'Identity provider is not configured.',
  roles: [],
  displayName: '',
  initials: '',
  signIn: () => {},
  signOut: () => {},
};

export function AuthProvider({ children }: { children: ReactNode }) {
  if (oidcEnabled) {
    return (
      <OidcProvider {...oidcConfig}>
        <OidcBridge>{children}</OidcBridge>
      </OidcProvider>
    );
  }
  if (import.meta.env.DEV) {
    return <DevAuthProvider>{children}</DevAuthProvider>;
  }
  return <AcmpAuthContext.Provider value={FAIL_CLOSED}>{children}</AcmpAuthContext.Provider>;
}
