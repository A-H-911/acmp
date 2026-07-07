/*
 * Route gates. ProtectedRoute requires an authenticated session; RequireRole
 * additionally requires one of the given roles, rendering an inline 403 state
 * otherwise (docs/domain/information-architecture.md page 91). These hide UI — the API is the real authority
 * (P4); a denied user must still get a 403 from the server, not just a hidden
 * link.
 */
import type { ReactNode } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth, hasRole } from './AcmpAuthContext';
import type { CommitteeRole } from './roles';
import { LoadingState, ErrorState, PermissionDenied } from '../components/states';

export function ProtectedRoute() {
  const auth = useAuth();
  const location = useLocation();
  if (auth.isLoading) return <LoadingState />;
  if (auth.error && !auth.isAuthenticated) return <ErrorState body={auth.error} />;
  // Carry where the user was headed to /login (a card deep link opens an unauthenticated tab) so the
  // sign-in returns them there, not to '/'.
  if (!auth.isAuthenticated)
    return <Navigate to="/login" state={{ from: location.pathname + location.search }} replace />;
  return <Outlet />;
}

export function RequireRole({ roles, children }: { roles: CommitteeRole[]; children?: ReactNode }) {
  const auth = useAuth();
  if (!hasRole(auth, ...roles)) return <PermissionDenied />;
  return children ? <>{children}</> : <Outlet />;
}
