/*
 * Top chrome (docs/14 §2, design "Navigation & IA"): brand, global search,
 * locale + theme toggles, notification bell, and the identity cluster — a
 * dropdown menu (design pattern: avatar + name + role → role="menu" panel).
 * Roles are read-only (sourced from Keycloak); the menu hosts account actions,
 * Sign out for now. Search is wired as a form; the results page is a later phase.
 */
import { useEffect, useRef, useState } from 'react';
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
  const [userOpen, setUserOpen] = useState(false);
  const [query, setQuery] = useState('');
  const userRef = useRef<HTMLDivElement>(null);

  const otherLang = i18n.language === 'ar' ? 'en' : 'ar';
  const roleLabel = roles[0] ? t(`role.${roles[0]}`) : '';

  // Close the account menu on Escape or click-away (mirrors NotificationCenter).
  useEffect(() => {
    if (!userOpen) return;
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setUserOpen(false);
    const onClick = (e: MouseEvent) => {
      if (userRef.current && !userRef.current.contains(e.target as Node)) setUserOpen(false);
    };
    document.addEventListener('keydown', onKey);
    document.addEventListener('mousedown', onClick);
    return () => {
      document.removeEventListener('keydown', onKey);
      document.removeEventListener('mousedown', onClick);
    };
  }, [userOpen]);

  const onSearch = (e: React.FormEvent) => {
    e.preventDefault();
    if (query.trim()) navigate(`/search?q=${encodeURIComponent(query.trim())}`);
  };

  return (
    <header className="topbar">
      <div className="topbar-brand">
        <span className="brand-badge"><Icon name="building" size={16} aria-hidden /></span>
        <span className="brand-word">{t('app.name')}</span>
      </div>

      <form className="search" role="search" onSubmit={onSearch}>
        <span className="search-icon"><Icon name="search" size={16} /></span>
        <input
          className="search-input"
          type="search"
          aria-label={t('common.search')}
          placeholder={t('common.search')}
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
          <span className="notif-dot" aria-hidden="true" />
        </button>
        <NotificationCenter open={notifOpen} onClose={() => setNotifOpen(false)} />
      </div>

      <button
        type="button"
        className="icon-btn"
        onClick={toggle}
        aria-label={t(theme === 'dark' ? 'common.light' : 'common.dark')}
      >
        <Icon name={theme === 'dark' ? 'sun' : 'moon'} size={17} />
      </button>

      <div className="user-menu" ref={userRef}>
        <button
          type="button"
          className="user-menu-trigger"
          aria-haspopup="menu"
          aria-expanded={userOpen}
          aria-label={t('auth.accountMenu')}
          onClick={() => setUserOpen((o) => !o)}
        >
          <span className="avatar" aria-hidden="true">{initials}</span>
          <span style={{ lineHeight: 1.1 }}>
            <span className="topbar-user-name">{displayName}</span>
            <span className="topbar-user-role">{roleLabel}</span>
          </span>
          <Icon name="chevron" size={14} className="user-menu-caret" aria-hidden />
        </button>

        {userOpen && (
          <div className="user-menu-panel" role="menu" aria-label={t('auth.accountMenu')}>
            <div className="user-menu-id">
              <span className="topbar-user-name">{displayName}</span>
              <span className="topbar-user-role">{roleLabel ? `${roleLabel} · ` : ''}{t('admin.fromKeycloak')}</span>
            </div>
            <div className="user-menu-sep" role="separator" />
            <button
              type="button"
              className="user-menu-item"
              role="menuitem"
              onClick={() => { setUserOpen(false); signOut(); }}
            >
              <Icon name="logout" size={16} />
              {t('auth.signOut')}
            </button>
          </div>
        )}
      </div>
    </header>
  );
}
