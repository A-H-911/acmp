import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import AdministrationPage from './AdministrationPage';
import { renderWithAuth } from '../test/render';
import type { Member } from '../api/members';
import type { SystemHealth } from '../api/systemHealth';

vi.mock('../api/members', () => ({ useMembers: vi.fn() }));
import { useMembers } from '../api/members';
const mockUseMembers = useMembers as unknown as Mock;

vi.mock('../api/systemHealth', () => ({ useSystemHealth: vi.fn() }));
import { useSystemHealth } from '../api/systemHealth';
const mockUseHealth = useSystemHealth as unknown as Mock;

vi.mock('../api/jobs', () => ({ useAdminJobs: vi.fn(), useRequeueJob: vi.fn() }));
import { useAdminJobs, useRequeueJob } from '../api/jobs';
const mockUseJobs = useAdminJobs as unknown as Mock;
const mockUseRequeue = useRequeueJob as unknown as Mock;

const MEMBERS: Member[] = [
  {
    publicId: '1', keycloakUserId: 'kc-fixture', fullName: 'Khalid A', email: 'khalid@acmp.gov', role: 'Secretary',
    status: 'Active', isActive: true, isVotingEligible: true,
    streams: [{ publicId: 's1', code: 'architecture', nameEn: 'Architecture', nameAr: 'الهندسة' }],
  },
];

const HEALTHY: SystemHealth = {
  status: 'Healthy',
  entries: [
    { name: 'api', status: 'Healthy', description: 'Serving requests', durationMs: 1.2 },
    { name: 'sqlserver', status: 'Healthy', description: null, durationMs: 8.3 },
  ],
};

function renderPage() {
  mockUseMembers.mockReturnValue({ data: MEMBERS, isLoading: false, isError: false, refetch: vi.fn() });
  mockUseHealth.mockReturnValue({ data: HEALTHY, isLoading: false, isError: false, refetch: vi.fn(), isFetching: false });
  mockUseJobs.mockReturnValue({
    data: { configured: true, counts: { succeeded: 3, processing: 0, scheduled: 0, enqueued: 0, failed: 0 }, jobs: [] },
    isLoading: false, isError: false, refetch: vi.fn(),
  });
  mockUseRequeue.mockReturnValue({ mutate: vi.fn(), isPending: false, isError: false, variables: undefined });
  renderWithAuth(<AdministrationPage />, { roles: ['administrator'] });
}

describe('AdministrationPage — sub-tab container', () => {
  beforeEach(() => {
    mockUseMembers.mockReset();
    mockUseHealth.mockReset();
    mockUseJobs.mockReset();
    mockUseRequeue.mockReset();
  });

  it('renders all seven sub-tabs, all navigable (design disables none)', () => {
    renderPage();
    const tabs = screen.getAllByRole('tab');
    expect(tabs).toHaveLength(7);
    expect(tabs.filter((t) => (t as HTMLButtonElement).disabled)).toHaveLength(0);
    expect(screen.getByRole('tab', { name: /Users & Membership/ })).toHaveAttribute('aria-selected', 'true');
  });

  it('defaults to the Users directory', () => {
    renderPage();
    expect(screen.getByText('Roles are read-only')).toBeInTheDocument();
    expect(screen.getByText('Khalid A')).toBeInTheDocument();
  });

  it('switches to System Health and renders live + unmonitored service tiles', async () => {
    const user = userEvent.setup();
    renderPage();
    await user.click(screen.getByRole('tab', { name: /System Health/ }));

    expect(screen.getByText('All core systems operational')).toBeInTheDocument();
    expect(screen.getByText('SQL Server')).toBeInTheDocument();
    expect(screen.getAllByText('Operational').length).toBeGreaterThan(0);
    // A service with no registered check is honest about it, not shown as down.
    expect(screen.getByText('MinIO object storage')).toBeInTheDocument();
    expect(screen.getAllByText('Monitoring not configured').length).toBeGreaterThan(0);
  });

  it('switches to Roles and renders the read-only Keycloak reference', async () => {
    const user = userEvent.setup();
    renderPage();
    await user.click(screen.getByRole('tab', { name: /Roles/ }));
    expect(screen.getByText(/Mirrored from Keycloak realm roles/)).toBeInTheDocument();
    expect(screen.getByText('Chairman')).toBeInTheDocument();
    expect(screen.getByText('Guest / Presenter')).toBeInTheDocument();
  });

  it('switches to Notification Settings and renders channels + a read-only matrix', async () => {
    const user = userEvent.setup();
    renderPage();
    await user.click(screen.getByRole('tab', { name: /Notification Settings/ }));
    expect(screen.getByRole('heading', { name: 'Default notifications by event type' })).toBeInTheDocument();
    expect(screen.getByText('Topic submitted')).toBeInTheDocument();
    // In-app toggles are presentational (no persistence backend yet).
    screen.getAllByRole('switch').forEach((s) => expect(s).toHaveAttribute('aria-disabled', 'true'));
    expect(screen.getAllByText('Planned').length).toBeGreaterThan(0);
  });

  it('shows honest-empty for templates / streams (later-phase modules)', async () => {
    const user = userEvent.setup();
    renderPage();
    await user.click(screen.getByRole('tab', { name: /Templates/ }));
    expect(screen.getByText('No templates yet')).toBeInTheDocument();
    await user.click(screen.getByRole('tab', { name: /Streams/ }));
    expect(screen.getByText('No streams configured')).toBeInTheDocument();
  });

  it('switches to Job Monitor and renders the live Hangfire stat tiles', async () => {
    const user = userEvent.setup();
    renderPage();
    await user.click(screen.getByRole('tab', { name: /Job Monitor/ }));
    expect(screen.getByText('No jobs yet')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument(); // succeeded tile
  });

  it('opening a user detail replaces the tabbed view (design userdetail sub-state)', async () => {
    const user = userEvent.setup();
    renderPage();
    await user.click(screen.getByRole('button', { name: 'View user detail' }));
    expect(screen.getByText('Back to users')).toBeInTheDocument();
    expect(screen.queryByRole('tablist')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Back to users' }));
    expect(screen.getByRole('tablist')).toBeInTheDocument();
    expect(screen.getByText('Khalid A')).toBeInTheDocument();
  });
});
