/*
 * Unauthenticated landing / sign-in route (docs/14 page 1). ACMP logo + name +
 * subtitle with a primary "Log in" button that starts the Keycloak auth-code +
 * PKCE redirect (no credentials handled in-app — entered on Keycloak; ADR-0015).
 * Keycloak redirects here after logout; we surface the signed-out / session-expired
 * status. Language + theme toggles stay available while signed out.
 */
import { useState } from 'react';
import { Navigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../auth/AcmpAuthContext';
import { consumeAuthStatus } from '../auth/authStatus';
import { useTheme } from '../theme/useTheme';
import { Icon } from '../components/icons';
import { Button } from '../components/ui/Button';
import { LoadingState } from '../components/states';

export function LoginPage() {
  const { t, i18n } = useTranslation();
  const { isAuthenticated, isLoading, signIn } = useAuth();
  const { theme, toggle } = useTheme();
  // Read once on mount: set by signOut / token-expiry before the redirect here.
  const [status] = useState(consumeAuthStatus);

  if (isLoading) return <LoadingState />;
  if (isAuthenticated) return <Navigate to="/dashboard" replace />;

  const otherLang = i18n.language === 'ar' ? 'en' : 'ar';
  const themeLabel = t(theme === 'dark' ? 'common.light' : 'common.dark');

  return (
    <div style={{ minBlockSize: '100vh', display: 'grid', placeItems: 'center', padding: 'var(--sp-6)' }}>
      <div className="card card-pad" style={{ inlineSize: 'min(420px, 100%)', textAlign: 'center' }}>
        <img className="brand-mark" src="/favicon.svg" alt="" width={48} height={48} style={{ marginInline: 'auto', marginBlockEnd: 'var(--sp-4)' }} />
        <h1 style={{ margin: '0 0 var(--sp-1)', fontSize: 20 }}>{t('app.name')}</h1>
        <p className="page-lead" style={{ marginBlockEnd: 'var(--sp-5)' }}>{t('app.tagline')}</p>

        {status && (
          <p role="status" className="login-status">
            {t(status === 'signed_out' ? 'auth.signedOut' : 'auth.sessionExpired')}
          </p>
        )}

        <Button onClick={signIn} style={{ inlineSize: '100%' }}>{t('auth.login')}</Button>

        <div className="login-toggles">
          <button type="button" className="btn btn-ghost" onClick={() => i18n.changeLanguage(otherLang)}>
            <Icon name="globe" size={15} />
            {otherLang === 'ar' ? 'العربية' : 'English'}
          </button>
          <button type="button" className="btn btn-ghost" onClick={toggle} aria-label={themeLabel}>
            <Icon name={theme === 'dark' ? 'sun' : 'moon'} size={15} />
            {themeLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
