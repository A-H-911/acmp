/*
 * OIDC callback handler (reconciled to ACMP System States `callback`, docs/domain/information-architecture.md
 * page 2). react-oidc-context processes the authorization code automatically;
 * this shows the secure-handoff spinner and routes onward once the session
 * resolves (or surfaces a token-exchange error).
 */
import { useEffect, useState } from 'react';
import { Navigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../auth/AcmpAuthContext';
import { RETURN_KEY } from '../auth/authConfig';
import { ErrorState } from '../components/states';

export function AuthCallbackPage() {
  const { t } = useTranslation();
  const { isLoading, isAuthenticated, error } = useAuth();
  // Captured once at mount (before the effect clears it) so StrictMode's double render can't drop the
  // deep link; the redirect below routes the user to it, defaulting to '/'.
  const [returnTo] = useState(() => sessionStorage.getItem(RETURN_KEY) ?? '/');
  useEffect(() => {
    if (isAuthenticated) sessionStorage.removeItem(RETURN_KEY);
  }, [isAuthenticated]);

  if (error) return <ErrorState title={t('auth.failedTitle')} body={error} />;
  if (isLoading) {
    return (
      <div className="page">
        <div className="state" role="status" aria-live="polite" aria-busy="true">
          <div className="spinner" aria-hidden />
          <p className="state-title state-title-lg">{t('auth.completing')}</p>
          <p className="state-body">{t('auth.completingBody')}</p>
          <p className="state-note">{t('auth.secureHandoff')}</p>
        </div>
      </div>
    );
  }
  return <Navigate to={isAuthenticated ? returnTo : '/login'} replace />;
}
