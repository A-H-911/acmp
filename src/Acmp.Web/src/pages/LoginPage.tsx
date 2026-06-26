/*
 * Unauthenticated landing / sign-in route (docs/14 page 1; design "ACMP Sign In").
 * ACMP logo + name + "Sign in to continue" with a primary Log in button that starts
 * the Keycloak auth-code + PKCE redirect (no credentials handled in-app — entered on
 * Keycloak; ADR-0015). Keycloak redirects here after logout; we surface the signed-out
 * / session-expired status as a tonal banner. Language + theme toggles stay available.
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
  const isWarn = status === 'session_expired';

  return (
    <div className="login-shell">
      <header className="login-header">
        <button type="button" className="login-ctl" onClick={() => i18n.changeLanguage(otherLang)}>
          <Icon name="globe" size={15} />
          {otherLang === 'ar' ? 'العربية' : 'English'}
        </button>
        <button type="button" className="login-ctl login-ctl-icon" onClick={toggle} aria-label={themeLabel}>
          <Icon name={theme === 'dark' ? 'sun' : 'moon'} size={16} />
        </button>
      </header>

      <main className="login-main">
        {status && (
          <div role="status" className={`login-status login-status-${isWarn ? 'warn' : 'info'}`}>
            <Icon name={isWarn ? 'risk' : 'logout'} size={17} aria-hidden />
            <span>{t(isWarn ? 'auth.sessionExpired' : 'auth.signedOut')}</span>
          </div>
        )}

        <div className="card card-pad login-card">
          <img className="brand-mark login-mark" src="/acmp-mark.svg" alt="" width={60} height={60} />
          <h1 className="login-title">{t('app.name')}</h1>
          <div className="login-appname">{t('app.tagline')}</div>
          <hr className="login-divider" />
          <p className="login-subtitle">{t('auth.subtitle')}</p>
          <Button className="login-cta" onClick={signIn}>
            <Icon name="login" size={18} className="dir-flip" aria-hidden />
            {t('auth.login')}
          </Button>
          <div className="login-secure">
            <Icon name="lock" size={13} aria-hidden />
            <span>{t('auth.secure')}</span>
          </div>
        </div>

        <p className="login-invite">{t('auth.invite')}</p>
      </main>
    </div>
  );
}
