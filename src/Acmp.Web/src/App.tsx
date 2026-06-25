import { Routes, Route, Navigate } from 'react-router-dom';
import Layout from './components/Layout';
import DashboardPage from './pages/DashboardPage';
import PlaceholderPage from './pages/PlaceholderPage';

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<Layout />}>
        <Route index element={<DashboardPage />} />
        <Route path="topics" element={<PlaceholderPage titleKey="nav.topics" />} />
        <Route path="meetings" element={<PlaceholderPage titleKey="nav.meetings" />} />
        <Route path="decisions" element={<PlaceholderPage titleKey="nav.decisions" />} />
        <Route path="actions" element={<PlaceholderPage titleKey="nav.actions" />} />
        <Route path="members" element={<PlaceholderPage titleKey="nav.members" />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  );
}
