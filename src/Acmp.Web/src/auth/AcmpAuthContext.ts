/*
 * Unified auth surface the app consumes, independent of whether the session
 * comes from real Keycloak/OIDC or the DEV-only stub. Components depend on this
 * shape — not on react-oidc-context directly — so the two backends are
 * interchangeable and the stub can never leak into a production bundle.
 */
import { createContext, useContext } from 'react';
import type { CommitteeRole } from './roles';

export interface AcmpAuth {
  isLoading: boolean;
  isAuthenticated: boolean;
  error?: string;
  roles: CommitteeRole[];
  displayName: string;
  initials: string;
  signIn: () => void;
  signOut: () => void;
  /** DEV-only role switcher for the shell preview; undefined in production. */
  devSetRoles?: (roles: CommitteeRole[]) => void;
}

export const AcmpAuthContext = createContext<AcmpAuth | null>(null);

export function useAuth(): AcmpAuth {
  const ctx = useContext(AcmpAuthContext);
  if (!ctx) throw new Error('useAuth must be used within <AuthProvider>');
  return ctx;
}

export function hasRole(auth: AcmpAuth, ...roles: CommitteeRole[]): boolean {
  return roles.some((r) => auth.roles.includes(r));
}
