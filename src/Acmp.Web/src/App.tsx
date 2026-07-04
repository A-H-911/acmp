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
import { MeetingPage, MeetingConduct } from './features/meetings/MeetingPage';
import { MeetingOverview } from './features/meetings/MeetingOverview';
import { AgendaBuilder } from './features/meetings/AgendaBuilder';
import { MeetingMinutes } from './features/meetings/MeetingMinutes';
import { MeetingRecording } from './features/meetings/MeetingRecording';
import { SchedulePage } from './features/meetings/SchedulePage';
import { DecisionPage } from './features/decisions/DecisionPage';
import { VotePage } from './features/voting/VotePage';
import { ActionsRegister } from './features/actions/ActionsRegister';
import { ActionPage } from './features/actions/ActionPage';
import { AdrsRegister } from './features/governance/AdrsRegister';
import { AdrPage } from './features/governance/AdrPage';
import { InvariantsRegister } from './features/governance/InvariantsRegister';
import { InvariantPage } from './features/governance/InvariantPage';
import { RisksRegister } from './features/risks/RisksRegister';
import { RiskPage } from './features/risks/RiskPage';
import { DependenciesRegister } from './features/dependencies/DependenciesRegister';
import { DependencyPage } from './features/dependencies/DependencyPage';
import { ImpactGraphPage } from './features/traceability/ImpactGraphPage';
import { ReportsPage } from './features/reports/ReportsPage';

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
        {/* Meeting shell (Meetings owns the chrome) + nested content surfaces (Agenda & Meeting owns
            agenda/conduct/minutes). Both /attendance and /notes render the conduct composition. */}
        <Route path="meetings/:key" element={<MeetingPage />}>
          <Route index element={<MeetingOverview />} />
          <Route path="agenda" element={<AgendaBuilder />} />
          <Route path="attendance" element={<MeetingConduct />} />
          <Route path="notes" element={<MeetingConduct />} />
          <Route path="minutes" element={<MeetingMinutes />} />
          <Route path="recording" element={<MeetingRecording />} />
        </Route>
        <Route path="decisions" element={<PlaceholderPage titleKey="nav.decisions" />} />
        <Route path="decisions/:key" element={<DecisionPage />} />
        <Route path="votes/:key" element={<VotePage />} />
        <Route path="actions" element={<ActionsRegister />} />
        <Route path="actions/:key" element={<ActionPage />} />
        <Route path="adrs" element={<AdrsRegister />} />
        <Route path="adrs/:key" element={<AdrPage />} />
        <Route path="invariants" element={<InvariantsRegister />} />
        <Route path="invariants/:key" element={<InvariantPage />} />
        <Route path="risks" element={<RisksRegister />} />
        <Route path="risks/:key" element={<RiskPage />} />
        <Route path="dependencies" element={<DependenciesRegister />} />
        <Route path="dependencies/:key" element={<DependencyPage />} />
        <Route path="traceability/:type/:key" element={<ImpactGraphPage />} />
        <Route path="research" element={<PlaceholderPage titleKey="nav.research" />} />
        <Route path="wiki" element={<PlaceholderPage titleKey="nav.wiki" />} />
        <Route path="diagrams" element={<PlaceholderPage titleKey="nav.diagrams" phase2 />} />
        <Route path="reports" element={<ReportsPage />} />
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
