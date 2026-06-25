/*
 * Top chrome (docs/14 §2, design "Navigation & IA"): brand, global search,
 * locale + theme toggles, notification bell, and the read-only role/identity
 * cluster. Present on every page. Search is wired as a form but the results
 * page is a later phase; here it routes to /search.
 */
import { useState } from 'react';
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
  const [query, setQuery] = useState('');

  const otherLang = i18n.language === 'ar' ? 'en' : 'ar';
  const roleLabel = roles[0] ? t(`role.${roles[0]}`) : '';

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

      <div className="topbar-user">
        <span className="avatar" aria-hidden="true">{initials}</span>
        <span style={{ lineHeight: 1.1 }}>
          <span className="topbar-user-name">{displayName}</span>
          <span className="topbar-user-role">{roleLabel}</span>
        </span>
      </div>

      <button
        type="button"
        className="icon-btn"
        onClick={signOut}
        aria-label={t('auth.signOut')}
      >
        <Icon name="logout" size={17} />
      </button>
    </header>
  );
}
