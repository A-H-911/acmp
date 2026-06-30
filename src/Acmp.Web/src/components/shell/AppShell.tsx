/*
 * Application shell: skip link → top chrome → (sidebar + routed main).
 * The main region is the Suspense/error host for page content; each page
 * supplies its own empty/loading/error states.
 */
import { Outlet, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ErrorBoundary } from '../ErrorBoundary';
import { Breadcrumb } from '../ui/Breadcrumb';
import { deriveBreadcrumbs } from '../../nav/breadcrumbs';
import { TopBar } from './TopBar';
import { SideNav } from './SideNav';

export function AppShell() {
  const { t } = useTranslation();
  const { pathname } = useLocation();
  const crumbs = deriveBreadcrumbs(pathname, t);
  return (
    <div className="app-shell">
      <a className="skip-link" href="#main">{t('common.skipToContent')}</a>
      <TopBar />
      <div className="app-body">
        <SideNav />
        <main className="app-main" id="main" tabIndex={-1}>
          <div className="shell-crumbs">
            <Breadcrumb items={crumbs} ariaLabel={t('common.breadcrumb')} />
          </div>
          <ErrorBoundary>
            <Outlet />
          </ErrorBoundary>
        </main>
      </div>
    </div>
  );
}
