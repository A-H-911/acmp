import { Routes, Route, Navigate } from 'react-router-dom';
import { AppShell } from './components/shell/AppShell';
import { ProtectedRoute, RequireRole } from './auth/ProtectedRoute';
import { LoginPage } from './pages/LoginPage';
import { AuthCallbackPage } from './pages/AuthCallbackPage';
import { NotFoundPage } from './pages/NotFoundPage';
import DashboardPage from './pages/DashboardPage';
import PlaceholderPage from './pages/PlaceholderPage';
import AdministrationPage from './pages/AdministrationPage';
import { Backlog } from './features/topics/Backlog';

/*
 * Route tree for the P3 shell. Every nav area resolves to a foundation
 * placeholder; feature screens arrive at their phases. Auth pages sit outside
 * the shell; everything else is behind ProtectedRoute. RequireRole guards the
 * admin area as the reference pattern for role-gated routes (UI gating only —
 * the API enforces, P4).
 */
export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/auth/callback" element={<AuthCallbackPage />} />

      <Route element={<ProtectedRoute />}>
        <Route path="/" element={<AppShell />}>
          <Route index element={<Navigate to="/dashboard" replace />} />
          <Route path="dashboard" element={<DashboardPage />} />
          <Route path="session" element={<PlaceholderPage titleKey="nav.session" />} />
          <Route path="topics/new" element={<PlaceholderPage titleKey="nav.submit" />} />
          <Route path="backlog" element={<Backlog />} />
          {/* Topic detail screen lands in P5b PR3; interim placeholder keeps backlog row links from 404ing. */}
          <Route path="topics/:key" element={<PlaceholderPage titleKey="nav.backlog" />} />
          <Route path="meetings" element={<PlaceholderPage titleKey="nav.agenda" />} />
          <Route path="decisions" element={<PlaceholderPage titleKey="nav.decisions" />} />
          <Route path="actions" element={<PlaceholderPage titleKey="nav.actions" />} />
          <Route path="adrs" element={<PlaceholderPage titleKey="nav.adrs" />} />
          <Route path="risks" element={<PlaceholderPage titleKey="nav.risks" />} />
          <Route path="dependencies" element={<PlaceholderPage titleKey="nav.deps" />} />
          <Route path="research" element={<PlaceholderPage titleKey="nav.research" />} />
          <Route path="knowledge" element={<PlaceholderPage titleKey="nav.wiki" />} />
          <Route path="diagrams" element={<PlaceholderPage titleKey="nav.diagrams" />} />
          <Route path="reports" element={<PlaceholderPage titleKey="nav.reports" />} />
          <Route path="search" element={<PlaceholderPage titleKey="common.search" />} />

          <Route path="admin" element={<RequireRole roles={['administrator']} />}>
            <Route index element={<AdministrationPage />} />
          </Route>
          <Route
            path="admin/audit"
            element={<RequireRole roles={['administrator', 'auditor', 'chairman']} />}
          >
            <Route index element={<PlaceholderPage titleKey="nav.audit" />} />
          </Route>

          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Route>
    </Routes>
  );
}
