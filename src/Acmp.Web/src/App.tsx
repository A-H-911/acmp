import { Route, Navigate, createRoutesFromElements } from 'react-router-dom';
import { AppShell } from './components/shell/AppShell';
import { ProtectedRoute, RequireRole } from './auth/ProtectedRoute';
import { LoginPage } from './pages/LoginPage';
import { AuthCallbackPage } from './pages/AuthCallbackPage';
import { NotFoundPage } from './pages/NotFoundPage';
import DashboardPage from './pages/DashboardPage';
import NotificationsPage from './pages/NotificationsPage';
import PlaceholderPage from './pages/PlaceholderPage';
import AdministrationPage from './pages/AdministrationPage';
import { Backlog } from './features/topics/Backlog';
import { SubmitTopic } from './features/topics/SubmitTopic';
import { TopicDetail } from './features/topics/TopicDetail';
import { MeetingsList } from './features/meetings/MeetingsList';
import { MeetingPage } from './features/meetings/MeetingPage';
import { SchedulePage } from './features/meetings/SchedulePage';

/*
 * Route tree for the app. Defined as a data-router config (createRoutesFromElements)
 * so route-aware hooks like useBlocker work (the unsaved-work guard on the Submit
 * form, AC-047). Auth pages sit outside the shell; everything else is behind
 * ProtectedRoute. RequireRole guards the admin area (UI gating only — the API enforces).
 */
export const appRoutes = createRoutesFromElements(
  <>
    <Route path="/login" element={<LoginPage />} />
    <Route path="/auth/callback" element={<AuthCallbackPage />} />

    <Route element={<ProtectedRoute />}>
      <Route path="/" element={<AppShell />}>
        <Route index element={<DashboardPage />} />
        {/* Legacy alias — keep deep links to /dashboard working; Home is now '/' (Usage Map §G). */}
        <Route path="dashboard" element={<Navigate to="/" replace />} />
        <Route path="notifications" element={<NotificationsPage />} />
        <Route path="session" element={<PlaceholderPage titleKey="nav.session" />} />
        <Route path="backlog" element={<Backlog />} />
        <Route path="backlog/submit" element={<SubmitTopic />} />
        <Route path="topics/:key" element={<TopicDetail />} />
        <Route path="meetings" element={<MeetingsList />} />
        <Route path="meetings/new" element={<SchedulePage />} />
        <Route path="meetings/:key" element={<MeetingPage />} />
        <Route path="decisions" element={<PlaceholderPage titleKey="nav.decisions" />} />
        <Route path="actions" element={<PlaceholderPage titleKey="nav.actions" />} />
        <Route path="adrs" element={<PlaceholderPage titleKey="nav.adrs" />} />
        <Route path="risks" element={<PlaceholderPage titleKey="nav.risks" />} />
        <Route path="dependencies" element={<PlaceholderPage titleKey="nav.deps" />} />
        <Route path="research" element={<PlaceholderPage titleKey="nav.research" />} />
        <Route path="wiki" element={<PlaceholderPage titleKey="nav.wiki" />} />
        <Route path="diagrams" element={<PlaceholderPage titleKey="nav.diagrams" phase2 />} />
        <Route path="reports" element={<PlaceholderPage titleKey="nav.reports" />} />
        <Route path="search" element={<PlaceholderPage titleKey="common.search" />} />

        <Route path="admin" element={<RequireRole roles={['administrator']} />}>
          <Route index element={<Navigate to="/admin/users" replace />} />
          <Route path="users" element={<AdministrationPage />} />
        </Route>
        <Route path="audit" element={<RequireRole roles={['administrator', 'auditor', 'chairman']} />}>
          <Route index element={<PlaceholderPage titleKey="nav.audit" />} />
        </Route>

        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Route>
  </>,
);
