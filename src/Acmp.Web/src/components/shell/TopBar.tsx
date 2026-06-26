/*
 * Top chrome (docs/14 §2; design "ACMP" app shell + "Navigation & IA"): brand,
 * global search, locale + theme toggles, notification bell, and the profile
 * menu — avatar + name + role trigger → role="menu" panel holding the identity
 * and a Log out item (OIDC end-session against the self-hosted Keycloak realm,
 * ADR-0015). Present on every page.
 */
import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../auth/AcmpAuthContext';
import { useTheme } from '../../theme/useTheme';
import { Icon } from '../icons';
import { NotificationCenter } from './NotificationCenter';
import { DevRoleSwitcher } from './DevRoleSwitcher';

export function TopBar() {
  const { t, i18n } = useTranslation();
  const { theme, toggle } = useTheme();
  const { displayName, initials, roles, signOut } = useAuth();
  const navigate = useNavigate();
  const [notifOpen, setNotifOpen] = useState(false);
  const [profileOpen, setProfileOpen] = useState(false);
  const [query, setQuery] = useState('');

  const otherLang = i18n.language === 'ar' ? 'en' : 'ar';
  const roleLabel = roles[0] ? t(`role.${roles[0]}`) : '';

  // Escape closes the profile menu; outside-click is caught by the backdrop.
  useEffect(() => {
    if (!profileOpen) return;
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setProfileOpen(false);
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [profileOpen]);

  const onSearch = (e: React.FormEvent) => {
    e.preventDefault();
    if (query.trim()) navigate(`/search?q=${encodeURIComponent(query.trim())}`);
  };

  // OIDC end-session (oidc-client-ts signoutRedirect): clears the local session/
  // tokens and redirects to the post-logout (login) route. See AuthProvider.
  const onLogout = () => {
    setProfileOpen(false);
    signOut();
  };

  return (
    <header className="topbar">
      <div className="topbar-brand">
        <img className="brand-mark" src="/favicon.svg" alt="" width={30} height={30} />
        <span className="brand-id">
          <span className="brand-word">{t('app.name')}</span>
          <span className="brand-sub">{t('app.committee')}</span>
        </span>
      </div>

      <form className="search" role="search" onSubmit={onSearch}>
        <span className="search-icon"><Icon name="search" size={16} /></span>
        <input
          className="search-input"
          type="search"
          aria-label={t('common.searchPlaceholder')}
          placeholder={t('common.searchPlaceholder')}
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
      </form>

      <div className="topbar-spacer" />

      <DevRoleSwitcher />

      <button
        type="button"
        className="chip-btn"
        onClick={() => i18n.changeLanguage(otherLang)}
        aria-label={t('common.switchTo', { lang: t(`common.${otherLang === 'ar' ? 'arabic' : 'english'}`) })}
      >
        <Icon name="globe" size={15} />
        {otherLang === 'ar' ? 'العربية' : 'EN'}
      </button>

      <button
        type="button"
        className="icon-btn"
        onClick={toggle}
        aria-label={t(theme === 'dark' ? 'common.light' : 'common.dark')}
      >
        <Icon name={theme === 'dark' ? 'sun' : 'moon'} size={17} />
      </button>

      <div style={{ position: 'relative' }}>
        <button
          type="button"
          className="icon-btn"
          style={{ position: 'relative' }}
          aria-label={t('notif.title')}
          aria-expanded={notifOpen}
          onClick={() => setNotifOpen((o) => !o)}
        >
          <Icon name="bell" size={16} />
        </button>
        <NotificationCenter open={notifOpen} onClose={() => setNotifOpen(false)} />
      </div>

      <div className="profile-menu">
        <button
          type="button"
          className="profile-trigger"
          aria-haspopup="menu"
          aria-expanded={profileOpen}
          aria-label={t('auth.accountMenu')}
          onClick={() => setProfileOpen((o) => !o)}
        >
          <span className="avatar" aria-hidden="true">{initials}</span>
          <span className="profile-trigger-id">
            <span className="topbar-user-name">{displayName}</span>
            <span className="topbar-user-role">{roleLabel}</span>
          </span>
          <Icon name="chevronDown" size={14} className="profile-trigger-caret" aria-hidden />
        </button>

        {profileOpen && (
          <>
            <div className="profile-backdrop" onClick={() => setProfileOpen(false)} aria-hidden="true" />
            <div className="profile-panel" role="menu" aria-label={displayName || t('auth.accountMenu')}>
              <div className="profile-id">
                <span className="avatar" aria-hidden="true">{initials}</span>
                <span className="profile-id-text">
                  <span className="profile-id-name">{displayName}</span>
                  <span className="profile-id-role">{roleLabel}</span>
                </span>
              </div>
              <div className="profile-sep" role="separator" />
              <button type="button" className="profile-item" role="menuitem" onClick={onLogout}>
                <Icon name="logout" size={17} className="dir-flip" aria-hidden />
                <span className="profile-item-label">{t('auth.logout')}</span>
              </button>
            </div>
          </>
        )}
      </div>
    </header>
  );
}
