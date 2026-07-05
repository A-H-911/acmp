import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import axe from 'axe-core';
import RoleDashboard from './RoleDashboard';
import type { CommitteeRole } from '../../auth/roles';
import type { TopicSummary } from '../../api/topics';
import type { ActionSummary } from '../../api/actions';
import type { MeetingSummary } from '../../api/meetings';
import type { DecisionSummary } from '../../api/decisions';
import type { VoteSummary } from '../../api/votes';
import type { MinutesSummary } from '../../api/minutes';
import type { RiskSummary } from '../../api/risks';

vi.mock('../../auth/AcmpAuthContext', async (orig) => ({ ...(await orig<typeof import('../../auth/AcmpAuthContext')>()), useAuth: vi.fn() }));
vi.mock('../../api/topics', async (orig) => ({ ...(await orig<typeof import('../../api/topics')>()), useBacklog: vi.fn() }));
vi.mock('../../api/meetings', async (orig) => ({ ...(await orig<typeof import('../../api/meetings')>()), useMeetings: vi.fn() }));
vi.mock('../../api/actions', async (orig) => ({ ...(await orig<typeof import('../../api/actions')>()), useActionsRegister: vi.fn() }));
vi.mock('../../api/decisions', async (orig) => ({ ...(await orig<typeof import('../../api/decisions')>()), useDecisionsRegister: vi.fn() }));
vi.mock('../../api/votes', async (orig) => ({ ...(await orig<typeof import('../../api/votes')>()), useVotesRegister: vi.fn() }));
vi.mock('../../api/minutes', async (orig) => ({ ...(await orig<typeof import('../../api/minutes')>()), useMinutesAwaiting: vi.fn() }));
vi.mock('../../api/risks', async (orig) => ({ ...(await orig<typeof import('../../api/risks')>()), useRisksRegister: vi.fn() }));

import { useAuth } from '../../auth/AcmpAuthContext';
import { useBacklog } from '../../api/topics';
import { useMeetings } from '../../api/meetings';
import { useActionsRegister } from '../../api/actions';
import { useDecisionsRegister } from '../../api/decisions';
import { useVotesRegister } from '../../api/votes';
import { useMinutesAwaiting } from '../../api/minutes';
import { useRisksRegister } from '../../api/risks';

const mockAuth = useAuth as unknown as Mock;
const mockBacklog = useBacklog as unknown as Mock;
const mockMeetings = useMeetings as unknown as Mock;
const mockActions = useActionsRegister as unknown as Mock;
const mockDecisions = useDecisionsRegister as unknown as Mock;
const mockVotes = useVotesRegister as unknown as Mock;
const mockMinutes = useMinutesAwaiting as unknown as Mock;
const mockRisks = useRisksRegister as unknown as Mock;

const paged = <T,>(items: T[]) => ({ data: { items, total: items.length, page: 1, pageSize: 500, totalPages: 1 }, isLoading: false, isError: false, refetch: vi.fn() });
const arr = <T,>(items: T[]) => ({ data: items, isLoading: false, isError: false, refetch: vi.fn() });
const authFor = (roles: CommitteeRole[]) => ({ isLoading: false, isAuthenticated: true, roles, userId: 'kc-1', displayName: 'Omar', initials: 'O', signIn: vi.fn(), signOut: vi.fn() });

const topic = (p: Partial<TopicSummary>): TopicSummary => ({ id: 'i', key: 'TOP-2026-001', title: 'Topic', type: 'ArchitectureDecision', status: 'Triage', urgency: 'Normal', scope: 'SingleStream', streams: ['identity'], ownerId: null, ownerName: null, priority: 0, timesDeferred: 0, ageDays: 1, slaBreached: false, createdAt: '2026-01-01', ...p });
const action = (p: Partial<ActionSummary>): ActionSummary => ({ id: 'i', key: 'ACT-2026-001', title: { en: 'Action', ar: 'x' }, status: 'Open', priority: 'Normal', ownerUserId: 'kc-1', ownerName: 'O', dueDate: null, isOverdue: false, progressPct: 0, sourceType: 'Topic', sourceId: 's', sourceKey: null, meetingKey: null, ...p });
const meeting = (p: Partial<MeetingSummary>): MeetingSummary => ({ id: 'i', key: 'MTG-2026-019', title: 'Weekly Architecture Committee', scheduledStart: '2099-02-18T10:00:00Z', scheduledEnd: '2099-02-18T11:30:00Z', status: 'Scheduled', type: 'Regular', mode: 'InPerson', chairName: 'Sara', itemCount: 4, agendaStatus: 'Draft', ...p });
const decision = (p: Partial<DecisionSummary>): DecisionSummary => ({ id: 'i', key: 'DECN-2026-008', topicId: 't', meetingId: null, outcome: 'Approved', status: 'Issued', title: { en: 'Adopt Keycloak', ar: 'x' }, issuedAt: '2026-06-01', ...p } as DecisionSummary);
const vote = (p: Partial<VoteSummary>): VoteSummary => ({ id: 'i', key: 'VOT-2026-010', topicId: 't', meetingId: null, status: 'Closed', options: ['approve'], allowAbstain: true, minPresent: 5, minCast: 4, openedAt: null, closedAt: '2026-06-01', ...p });
const minutes = (p: Partial<MinutesSummary>): MinutesSummary => ({ id: 'i', key: 'MIN-2026-018', version: 1, meetingId: 'm', meetingKey: 'MTG-2026-018', status: 'InReview', publishedAt: null, ...p });
const risk = (p: Partial<RiskSummary>): RiskSummary => ({ id: 'i', key: 'RSK-2026-001', title: { en: 'Dual-run auth risk', ar: 'x' }, status: 'Escalated', likelihood: 'High', impact: 'High', severity: 9, exposure: 'Critical', ownerUserId: '', ownerName: '', subjectType: 'Topic', subjectId: 't', subjectKey: 'TOP-1', ...p });

function setup() {
  return render(<MemoryRouter><RoleDashboard /></MemoryRouter>);
}

beforeEach(() => {
  [mockAuth, mockBacklog, mockMeetings, mockActions, mockDecisions, mockVotes, mockMinutes, mockRisks].forEach((m) => m.mockReset());
  mockAuth.mockReturnValue(authFor(['member']));
  mockBacklog.mockReturnValue(paged<TopicSummary>([]));
  mockMeetings.mockReturnValue(arr<MeetingSummary>([]));
  mockActions.mockReturnValue(paged<ActionSummary>([]));
  mockDecisions.mockReturnValue(arr<DecisionSummary>([]));
  mockVotes.mockReturnValue(arr<VoteSummary>([]));
  mockMinutes.mockReturnValue(arr<MinutesSummary>([]));
  mockRisks.mockReturnValue(paged<RiskSummary>([]));
});

describe('RoleDashboard — role gating', () => {
  it('shows the committee variant for a plain member (AC-064 cards)', () => {
    setup();
    expect(screen.getByText('Here’s where the committee stands today.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Backlog health' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Next meeting' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Action status' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Recent decisions' })).toBeInTheDocument();
    // committee variant does not render secretary/chairman-only cards
    expect(screen.queryByRole('heading', { name: 'Secretary queue' })).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Votes awaiting approval' })).not.toBeInTheDocument();
  });

  it('falls back to the committee variant for a non-dashboard role (auditor)', () => {
    mockAuth.mockReturnValue(authFor(['auditor']));
    setup();
    expect(screen.getByRole('heading', { name: 'Backlog health' })).toBeInTheDocument();
  });

  it('shows the secretary variant for a secretary (AC-065 cards)', () => {
    mockAuth.mockReturnValue(authFor(['secretary']));
    setup();
    expect(screen.getByRole('heading', { name: 'Secretary queue' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Topics over SLA' })).toBeInTheDocument();
  });

  it('shows the chairman variant, and chairman wins over a second role (precedence)', () => {
    mockAuth.mockReturnValue(authFor(['member', 'chairman']));
    setup();
    expect(screen.getByRole('heading', { name: 'Votes awaiting approval' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Escalated risks' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Escalated actions' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Deferred ≥ 2 times' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Backlog health' })).not.toBeInTheDocument();
  });
});

describe('RoleDashboard — live data (committee, AC-064)', () => {
  it('renders backlog total, action counts, next meeting and last decisions', () => {
    mockBacklog.mockReturnValue(paged([topic({ status: 'Triage' }), topic({ key: 'TOP-2', status: 'Accepted', urgency: 'Critical' })]));
    mockMeetings.mockReturnValue(arr([meeting({})]));
    mockActions.mockReturnValue(paged([action({ status: 'Blocked', isOverdue: true })]));
    mockDecisions.mockReturnValue(arr([decision({})]));
    setup();
    expect(screen.getByText('2')).toBeInTheDocument(); // backlog total
    expect(screen.getByText('Weekly Architecture Committee')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Open agenda' })).toHaveAttribute('href', '/meetings/MTG-2026-019/agenda');
    expect(screen.getByText('Adopt Keycloak')).toBeInTheDocument();
    expect(screen.getByText('DECN-2026-008')).toBeInTheDocument();
  });

  it('renders honest empty states when every register is dry', () => {
    setup();
    expect(screen.getByText('No upcoming meeting scheduled.')).toBeInTheDocument();
    expect(screen.getByText('No decisions issued yet.')).toBeInTheDocument();
    expect(screen.getByText('active topics')).toBeInTheDocument();
  });
});

describe('RoleDashboard — live data (secretary, AC-065)', () => {
  it('renders the queue counts and the SLA-breached list with aging', () => {
    mockAuth.mockReturnValue(authFor(['secretary']));
    mockBacklog.mockReturnValue(paged([
      topic({ key: 'TOP-A', status: 'Triage' }),
      topic({ key: 'TOP-B', status: 'Triage', slaBreached: true, ageDays: 42 }),
    ]));
    mockMinutes.mockReturnValue(arr([minutes({}), minutes({ key: 'MIN-2' })]));
    setup();
    expect(screen.getByText('Awaiting triage')).toBeInTheDocument();
    expect(screen.getByText('Minutes to approve')).toBeInTheDocument();
    expect(screen.getByText('42d aging')).toBeInTheDocument();
    expect(screen.getByText('TOP-B')).toBeInTheDocument();
  });

  it('shows an empty SLA note when nothing breached', () => {
    mockAuth.mockReturnValue(authFor(['secretary']));
    setup();
    expect(screen.getByText('No topics past their SLA.')).toBeInTheDocument();
  });
});

describe('RoleDashboard — live data (chairman, AC-066)', () => {
  it('renders awaiting votes, escalated risks/actions and deferred≥2', () => {
    mockAuth.mockReturnValue(authFor(['chairman']));
    mockVotes.mockReturnValue(arr([vote({})]));
    mockRisks.mockReturnValue(paged([risk({})]));
    mockActions.mockReturnValue(paged([action({ key: 'ACT-2026-033', dueDate: '2020-01-01', isOverdue: true })]));
    mockBacklog.mockReturnValue(paged([topic({ key: 'TOP-2026-014', title: 'Deferred topic', timesDeferred: 3 })]));
    setup();
    expect(screen.getByText('Closed vote — awaiting your approval')).toBeInTheDocument();
    expect(screen.getByText('Dual-run auth risk')).toBeInTheDocument();
    expect(screen.getByText('Critical')).toBeInTheDocument(); // exposure chip
    expect(screen.getByText('Deferred topic')).toBeInTheDocument();
    expect(screen.getByText('3×')).toBeInTheDocument(); // deferred-times badge
  });

  it('shows empty notes when the chairman queues are clear', () => {
    mockAuth.mockReturnValue(authFor(['chairman']));
    setup();
    expect(screen.getByText('No votes awaiting your approval.')).toBeInTheDocument();
    expect(screen.getByText('No escalated risks.')).toBeInTheDocument();
    expect(screen.getByText('No escalated actions.')).toBeInTheDocument();
    expect(screen.getByText('No topics deferred twice or more.')).toBeInTheDocument();
  });
});

describe('RoleDashboard — states', () => {
  it('shows the loading state while any read is pending', () => {
    mockBacklog.mockReturnValue({ data: undefined, isLoading: true, isError: false, refetch: vi.fn() });
    setup();
    expect(screen.getByText('Loading dashboard…')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Backlog health' })).not.toBeInTheDocument();
  });

  it('shows the error state and refetches on retry', async () => {
    const refetch = vi.fn();
    mockActions.mockReturnValue({ data: undefined, isLoading: false, isError: true, refetch });
    setup();
    expect(screen.getByText('Couldn’t load the dashboard')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /retry/i }));
    expect(refetch).toHaveBeenCalled();
  });

  it('wires the RTL chevron affordance on list rows', () => {
    mockDecisions.mockReturnValue(arr([decision({})]));
    const { container } = setup();
    expect(container.querySelector('.dir-flip')).toBeInTheDocument();
  });

  it('is axe-clean with data loaded', async () => {
    mockBacklog.mockReturnValue(paged([topic({})]));
    mockMeetings.mockReturnValue(arr([meeting({})]));
    mockActions.mockReturnValue(paged([action({})]));
    mockDecisions.mockReturnValue(arr([decision({})]));
    const { container } = setup();
    const results = await axe.run(container);
    expect(results.violations).toEqual([]);
  });
});
