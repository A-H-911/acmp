/*
 * OIDC entry point (docs/14 page 1). Initiates the Keycloak auth-code+PKCE
 * redirect; no credentials are handled in-app. Already-authenticated users
 * (incl. the DEV stub) are bounced straight to the dashboard.
 */
import { Navigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../auth/AcmpAuthContext';
import { Icon } from '../components/icons';
import { Button } from '../components/ui/Button';
import { LoadingState } from '../components/states';

export function LoginPage() {
  const { t, i18n } = useTranslation();
  const { isAuthenticated, isLoading, signIn } = useAuth();

  if (isLoading) return <LoadingState />;
  if (isAuthenticated) return <Navigate to="/dashboard" replace />;

  const otherLang = i18n.language === 'ar' ? 'en' : 'ar';

  return (
    <div style={{ minBlockSize: '100vh', display: 'grid', placeItems: 'center', padding: 'var(--sp-6)' }}>
      <div className="card card-pad" style={{ inlineSize: 'min(420px, 100%)', textAlign: 'center' }}>
        <span className="brand-badge" style={{ inlineSize: 40, blockSize: 40, margin: '0 auto var(--sp-4)' }}>
          <Icon name="building" size={22} aria-hidden />
        </span>
        <h1 style={{ margin: '0 0 var(--sp-1)', fontSize: 20 }}>{t('app.name')}</h1>
        <p className="page-lead" style={{ marginBlockEnd: 'var(--sp-5)' }}>{t('app.tagline')}</p>
        <Button onClick={signIn} style={{ inlineSize: '100%' }}>{t('auth.signIn')}</Button>
        <button
          type="button"
          className="btn btn-ghost"
          style={{ marginBlockStart: 'var(--sp-3)' }}
          onClick={() => i18n.changeLanguage(otherLang)}
        >
          <Icon name="globe" size={15} />
          {otherLang === 'ar' ? 'العربية' : 'English'}
        </button>
      </div>
    </div>
  );
}
