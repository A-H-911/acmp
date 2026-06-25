import { useEffect, useState } from 'react';
import { NavLink, Outlet } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { applyTheme, getStoredTheme, type Theme } from '../theme/theme';

const NAV = ['dashboard', 'topics', 'meetings', 'decisions', 'actions', 'members'] as const;
const PATHS: Record<(typeof NAV)[number], string> = {
  dashboard: '/',
  topics: '/topics',
  meetings: '/meetings',
  decisions: '/decisions',
  actions: '/actions',
  members: '/members',
};

export default function Layout() {
  const { t, i18n } = useTranslation();
  const [theme, setTheme] = useState<Theme>(getStoredTheme());

  useEffect(() => {
    applyTheme(theme);
  }, [theme]);

  const toggleLanguage = () => i18n.changeLanguage(i18n.language === 'ar' ? 'en' : 'ar');
  const toggleTheme = () => setTheme((prev) => (prev === 'dark' ? 'light' : 'dark'));

  return (
    <div className="app-shell">
      <header className="app-header">
        <div className="brand">
          <span className="brand-mark">{t('app.name')}</span>
          <span className="brand-tagline">{t('app.tagline')}</span>
        </div>
        <div className="header-controls">
          <button type="button" className="control" onClick={toggleLanguage} aria-label={t('common.language')}>
            {i18n.language === 'ar' ? 'EN' : 'ع'}
          </button>
          <button type="button" className="control" onClick={toggleTheme} aria-label={t('common.theme')}>
            {theme === 'dark' ? t('common.light') : t('common.dark')}
          </button>
        </div>
      </header>
      <div className="app-body">
        <nav className="app-nav" aria-label={t('nav.dashboard')}>
          {NAV.map((key) => (
            <NavLink key={key} to={PATHS[key]} end={key === 'dashboard'} className="nav-link">
              {t(`nav.${key}`)}
            </NavLink>
          ))}
        </nav>
        <main className="app-main">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
