import { Route, Navigate, createRoutesFromElements } from 'react-router-dom';
import { AppShell } from './components/shell/AppShell';
import { ProtectedRoute, RequireRole } from './auth/ProtectedRoute';
import { LoginPage } from './pages/LoginPage';
import { AuthCallbackPage } from './pages/AuthCallbackPage';
import { NotFoundPage } from './pages/NotFoundPage';
import DashboardPage from './pages/DashboardPage';
import NotificationsPage from './pages/NotificationsPage';
import PlaceholderPage from './pages/PlaceholderPage';
import SessionPage from './features/session/SessionPage';
import SessionPreviewPage from './features/session/SessionPreviewPage';
import AdministrationPage from './pages/AdministrationPage';
import MembersPage from './pages/MembersPage';
import { Backlog } from './features/topics/Backlog';
import { SubmitTopic } from './features/topics/SubmitTopic';
import { TopicDetail } from './features/topics/TopicDetail';
import { EditTopic } from './features/topics/EditTopic';
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
import { ResearchRegister } from './features/research/ResearchRegister';
import { MissionPage } from './features/research/MissionPage';
import { SearchPage } from './features/search/SearchPage';
import { WikiPage } from './features/wiki/WikiPage';
import { TemplatesRegister } from './features/templates/TemplatesRegister';
import { DependenciesRegister } from './features/dependencies/DependenciesRegister';
import { DependencyPage } from './features/dependencies/DependencyPage';
import { ImpactGraphPage } from './features/traceability/ImpactGraphPage';
import { ReportsPage } from './features/reports/ReportsPage';
import { AuditRegister } from './features/audit/AuditRegister';

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
        {/* FR-159 / DEC-037 — the guest presenter surface, restricted to Guest plus Chairman and
            Secretary (preview). DEF-053: the API half always enforced this (both queries carry
            AllowedRoles and SessionApiTests forces a 403 for the other five roles), but the route
            did not, so a Member typing /session met "you are not presenting" — a true-sounding
            answer to a question they were not allowed to ask. The API stays the authority; this
            gate is what makes the refusal say what it means. */}
        <Route
          path="session"
          element={
            <RequireRole roles={['guest', 'chairman', 'secretary']}>
              <SessionPage />
            </RequireRole>
          }
        />
        {/* FR-165 / DEC-086 d1 — the presenter preview is its OWN route, guarded to the two roles that
            run the meeting. It cannot be a mode of /session: that route must stay open to Guests, so its
            guard can never refuse one, and a preview rendered there would have no route-level protection
            at all. This is layer 1 of three; the path gate and the query's role set are the other two. */}
        <Route
          path="session/preview"
          element={
            <RequireRole roles={['chairman', 'secretary']}>
              <SessionPreviewPage />
            </RequireRole>
          }
        />
        <Route path="backlog" element={<Backlog />} />
        <Route path="backlog/submit" element={<SubmitTopic />} />
        <Route path="topics/:key" element={<TopicDetail />} />
        <Route path="topics/:key/edit" element={<EditTopic />} />
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
        <Route path="research" element={<ResearchRegister />} />
        <Route path="research/:key" element={<MissionPage />} />
        <Route path="wiki" element={<WikiPage />} />
        <Route path="wiki/:key" element={<WikiPage />} />
        <Route path="templates" element={<TemplatesRegister />} />
        {/* DEC-028 (2026-07-17): P14 deferred INDEFINITELY — ACMP ships no in-product diagram
            renderer. The route stays so existing links do not 404, but it must not promise a phase. */}
        <Route path="diagrams" element={<PlaceholderPage titleKey="nav.diagrams" deferred />} />
        <Route path="reports" element={<ReportsPage />} />
        <Route path="search" element={<SearchPage />} />

        {/* OQ-069 / DEC-041 — the roster, invite and role assignment admit Administrator AND
            Secretary, which is what FR-156/FR-157 say and what the API has always enforced. The
            guard here is courtesy; the API is what refuses. */}
        <Route path="members" element={<RequireRole roles={['administrator', 'secretary']} />}>
          <Route index element={<MembersPage />} />
        </Route>
        <Route path="admin" element={<RequireRole roles={['administrator']} />}>
          <Route index element={<Navigate to="/admin/users" replace />} />
          <Route path="users" element={<AdministrationPage />} />
        </Route>
        {/* Audit read = {Auditor, Chairman, Secretary}; Administrator excluded on SoD-5 (ADR-0027,
            supersedes the FR-153 role clause). UI gating only — the API enforces Policies.AuditRead. */}
        <Route path="audit" element={<RequireRole roles={['auditor', 'chairman', 'secretary']} />}>
          <Route index element={<AuditRegister />} />
        </Route>

        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Route>
  </>,
);
