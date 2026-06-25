/*
 * OIDC callback handler (docs/14 page 2). react-oidc-context processes the
 * authorization code automatically; this page shows a loading state and routes
 * onward once the session resolves (or surfaces a token-exchange error).
 */
import { Navigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../auth/AcmpAuthContext';
import { LoadingState, ErrorState } from '../components/states';

export function AuthCallbackPage() {
  const { t } = useTranslation();
  const { isLoading, isAuthenticated, error } = useAuth();

  if (isLoading) return <LoadingState label={t('auth.completing')} />;
  if (error) return <ErrorState title={t('auth.failedTitle')} body={error} />;
  return <Navigate to={isAuthenticated ? '/dashboard' : '/login'} replace />;
}
