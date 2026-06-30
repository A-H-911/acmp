/*
 * Automated accessibility gate (axe-core) across the design-fidelity surfaces.
 * Asserts zero WCAG 2.0/2.1/2.2 A+AA violations for structure, roles, names, and
 * ARIA. color-contrast is excluded here because jsdom has no layout engine to
 * compute it; token contrast is verified separately as a byte-match to the design
 * tokens (and by the P3 live-axe pass). This runs in CI on every change.
 */
import { describe, it, expect, vi, type Mock } from 'vitest';
import axe from 'axe-core';
import { renderWithAuth, makeAuth } from './render';
import { LoginPage } from '../pages/LoginPage';
import { TopBar } from '../components/shell/TopBar';
import { SideNav } from '../components/shell/SideNav';
import { UsersDirectory } from '../features/administration/UsersMembership';
import { Backlog } from '../features/topics/Backlog';
import type { Member } from '../api/members';
import type { TopicSummary } from '../api/topics';

vi.mock('../api/members', () => ({ useMembers: vi.fn() }));
import { useMembers } from '../api/members';
const mockUseMembers = useMembers as unknown as Mock;

vi.mock('../api/topics', () => ({ useBacklog: vi.fn() }));
import { useBacklog } from '../api/topics';
const mockUseBacklog = useBacklog as unknown as Mock;

// TopBar reads the notification feed for the bell badge (renderWithAuth has no QueryClientProvider).
vi.mock('../api/notifications', () => ({
  useNotifications: vi.fn(() => ({ data: { items: [], unreadCount: 2 } })),
  useMarkNotificationRead: vi.fn(() => ({ mutate: vi.fn() })),
}));

const TOPICS: TopicSummary[] = [
  {
    id: 'g1', key: 'TOP-2026-014', title: 'Adopt Keycloak as the standard IdP', type: 'ArchitectureDecision',
    status: 'Scheduled', urgency: 'Urgent', scope: 'MultiStream', streams: ['identity', 'platform'],
    ownerId: 'o1', ownerName: 'Omar H', priority: 1, ageDays: 9, slaBreached: true, createdAt: '2026-02-15T09:00:00Z',
  },
  {
    id: 'g2', key: 'TOP-2026-031', title: 'Event streaming spike', type: 'ResearchDiscovery',
    status: 'Triage', urgency: 'Normal', scope: 'SingleStream', streams: ['notifications'],
    ownerId: null, ownerName: null, priority: 5, ageDays: 4, slaBreached: false, createdAt: '2026-02-20T09:00:00Z',
  },
];

const WCAG_AA = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'];

async function violations(container: HTMLElement) {
  const results = await axe.run(container, {
    runOnly: { type: 'tag', values: WCAG_AA },
    rules: { 'color-contrast': { enabled: false } },
  });
  return results.violations.map((v) => `${v.id}: ${v.help}`);
}

const MEMBERS: Member[] = [
  {
    publicId: '1', fullName: 'Khalid A', email: 'khalid@acmp.gov', role: 'Secretary',
    status: 'Active', isActive: true, isVotingEligible: true,
    streams: [{ publicId: 's1', code: 'architecture', nameEn: 'Architecture', nameAr: 'الهندسة' }],
  },
  {
    publicId: '2', fullName: 'Audit Office', email: 'audit@acmp.gov', role: 'Auditor',
    status: 'Active', isActive: true, isVotingEligible: false, streams: [],
  },
];

describe('Accessibility — axe-core (WCAG 2.2 AA structure/ARIA)', () => {
  it('Login page is axe-clean', async () => {
    const { container } = renderWithAuth(<LoginPage />, { auth: makeAuth([], { isAuthenticated: false }) });
    expect(await violations(container)).toEqual([]);
  });

  it('TopBar (shell chrome) is axe-clean', async () => {
    const { container } = renderWithAuth(<TopBar />, { roles: ['administrator'] });
    expect(await violations(container)).toEqual([]);
  });

  it('SideNav is axe-clean', async () => {
    const { container } = renderWithAuth(<SideNav />, { roles: ['administrator'] });
    expect(await violations(container)).toEqual([]);
  });

  it('Admin Users & Membership is axe-clean', async () => {
    mockUseMembers.mockReturnValue({ data: MEMBERS, isLoading: false, isError: false, refetch: vi.fn() });
    const { container } = renderWithAuth(<UsersDirectory onView={vi.fn()} />, { roles: ['administrator'] });
    expect(await violations(container)).toEqual([]);
  });

  it('Backlog (table view, live data) is axe-clean', async () => {
    mockUseBacklog.mockReturnValue({
      data: { items: TOPICS, total: 2, page: 1, pageSize: 25, totalPages: 1 },
      isLoading: false, isError: false, refetch: vi.fn(),
    });
    const { container } = renderWithAuth(<Backlog />, { roles: ['secretary'] });
    expect(await violations(container)).toEqual([]);
  });
});
