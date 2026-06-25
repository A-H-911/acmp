/*
 * Route gates. ProtectedRoute requires an authenticated session; RequireRole
 * additionally requires one of the given roles, rendering an inline 403 state
 * otherwise (docs/14 page 91). These hide UI — the API is the real authority
 * (P4); a denied user must still get a 403 from the server, not just a hidden
 * link.
 */
import type { ReactNode } from 'react';
import { Navigate, Outlet } from 'react-router-dom';
import { useAuth, hasRole } from './AcmpAuthContext';
import type { CommitteeRole } from './roles';
import { LoadingState, ErrorState, PermissionDenied } from '../components/states';

export function ProtectedRoute() {
  const auth = useAuth();
  if (auth.isLoading) return <LoadingState />;
  if (auth.error && !auth.isAuthenticated) return <ErrorState body={auth.error} />;
  if (!auth.isAuthenticated) return <Navigate to="/login" replace />;
  return <Outlet />;
}

export function RequireRole({ roles, children }: { roles: CommitteeRole[]; children?: ReactNode }) {
  const auth = useAuth();
  if (!hasRole(auth, ...roles)) return <PermissionDenied />;
  return children ? <>{children}</> : <Outlet />;
}
