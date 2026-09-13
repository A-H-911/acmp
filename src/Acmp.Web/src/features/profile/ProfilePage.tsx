/*
 * /profile — "ACMP System States.dc.html" `profile` (L84–110, copy table `t` L193–207). Identity card,
 * Preferences (language + theme, the same switches TopBar drives) and Account (SSO note, notification
 * preferences, log out). The breadcrumb is the shell's (nav/breadcrumbs.ts).
 *
 * Streams are not shown: no current-user stream membership reaches the client (roles.ts explains why
 * POST /members/me's response is discarded, and /members is admin/secretary-only).
 */
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../auth/AcmpAuthContext';
import { COMMITTEE_ROLES } from '../../auth/roles';
import { useTheme } from '../../theme/useTheme';
import type { Theme } from '../../theme/theme';
import { Segmented } from '../../components/ui/Segmented';
import { Button } from '../../components/ui/Button';
import { Icon } from '../../components/icons';
import './profile.css';

export function ProfilePage() {
  const { t, i18n } = useTranslation();
  const { displayName, initials, email, roles, signOut } = useAuth();
  const { theme, setTheme } = useTheme();
  const navigate = useNavigate();
  // Precedence order (primaryRoleOf's), never claim order (DEF-034).
  const heldRoles = COMMITTEE_ROLES.filter((r) => roles.includes(r));

  return (
    <div className="page">
      <div className="prof-wrap">
        <div className="card prof-id">
          <span className="avatar prof-avatar" aria-hidden="true">{initials}</span>
          <div className="prof-id-text">
            <h1 className="prof-name">{displayName}</h1>
            {email && <div className="prof-email">{email}</div>}
            {heldRoles.length > 0 && (
              <div className="prof-tags">
                {heldRoles.map((r) => (
                  <span key={r} className="prof-tag prof-tag-info">{t(`role.${r}`)}</span>
                ))}
              </div>
            )}
          </div>
        </div>

        <div className="prof-grid">
          <section className="card prof-card" aria-labelledby="prof-prefs">
            <h2 id="prof-prefs" className="prof-flabel">{t('profile.prefsTitle')}</h2>
            <div className="prof-seg">
              <span className="prof-lab">{t('profile.language')}</span>
              <Segmented
                ariaLabel={t('profile.language')}
                value={i18n.language === 'ar' ? 'ar' : 'en'}
                onValueChange={(lang) => void i18n.changeLanguage(lang)}
                items={[
                  { id: 'en', label: t('common.english') },
                  { id: 'ar', label: t('common.arabic') },
                ]}
              />
            </div>
            <div className="prof-seg">
              <span className="prof-lab">{t('profile.theme')}</span>
              <Segmented
                ariaLabel={t('profile.theme')}
                value={theme}
                onValueChange={(v) => setTheme(v as Theme)}
                items={[
                  { id: 'light', label: <><Icon name="sun" size={13} aria-hidden />{t('profile.light')}</> },
                  { id: 'dark', label: <><Icon name="moon" size={13} aria-hidden />{t('profile.dark')}</> },
                ]}
              />
            </div>
          </section>

          <section className="card prof-card" aria-labelledby="prof-account">
            <h2 id="prof-account" className="prof-flabel">{t('profile.accountTitle')}</h2>
            <div className="prof-account">
              <div className="prof-sso">
                <Icon name="lock" size={16} aria-hidden />
                <span>{t('profile.ssoNote')}</span>
              </div>
              <Button variant="secondary" className="prof-btn" onClick={() => navigate('/profile/preferences')}>
                <Icon name="bell" size={15} aria-hidden />
                {t('notifPrefs.title')}
              </Button>
              <Button variant="secondary" className="prof-btn prof-logout" onClick={signOut}>
                <Icon name="logout" size={15} className="dir-flip" aria-hidden />
                {t('auth.logout')}
              </Button>
            </div>
          </section>
        </div>
      </div>
    </div>
  );
}
