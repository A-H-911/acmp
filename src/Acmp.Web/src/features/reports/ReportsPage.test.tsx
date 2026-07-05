import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import axe from 'axe-core';
import { ReportsPage } from './ReportsPage';
import type { TopicSummary } from '../../api/topics';
import type { DecisionSummary } from '../../api/decisions';
import type { ActionSummary } from '../../api/actions';
import type { RiskSummary } from '../../api/risks';
import type { DependencySummary } from '../../api/dependencies';

vi.mock('../../api/topics', async (o) => ({ ...(await o<typeof import('../../api/topics')>()), useBacklog: vi.fn() }));
vi.mock('../../api/decisions', async (o) => ({ ...(await o<typeof import('../../api/decisions')>()), useDecisionsRegister: vi.fn() }));
vi.mock('../../api/actions', async (o) => ({ ...(await o<typeof import('../../api/actions')>()), useActionsRegister: vi.fn() }));
vi.mock('../../api/risks', async (o) => ({ ...(await o<typeof import('../../api/risks')>()), useRisksRegister: vi.fn() }));
vi.mock('../../api/dependencies', async (o) => ({ ...(await o<typeof import('../../api/dependencies')>()), useDependenciesRegister: vi.fn() }));

import { useBacklog } from '../../api/topics';
import { useDecisionsRegister } from '../../api/decisions';
import { useActionsRegister } from '../../api/actions';
import { useRisksRegister } from '../../api/risks';
import { useDependenciesRegister } from '../../api/dependencies';

const mBacklog = useBacklog as unknown as Mock;
const mDecisions = useDecisionsRegister as unknown as Mock;
const mActions = useActionsRegister as unknown as Mock;
const mRisks = useRisksRegister as unknown as Mock;
const mDeps = useDependenciesRegister as unknown as Mock;

const paged = <T,>(items: T[]) => ({ data: { items, total: items.length, page: 1, pageSize: 500, totalPages: 1 }, isLoading: false, isError: false, refetch: vi.fn() });
const arr = <T,>(items: T[]) => ({ data: items, isLoading: false, isError: false, refetch: vi.fn() });

const topic = (p: Partial<TopicSummary>): TopicSummary => ({ id: 'i', key: 'TOP-1', title: 't', type: 'ArchitectureDecision', status: 'Triage', urgency: 'Normal', scope: 'SingleStream', streams: ['identity'], ownerId: null, ownerName: null, priority: 0, timesDeferred: 0, ageDays: 40, slaBreached: false, createdAt: '2026-01-01', ...p });
const decision = (p: Partial<DecisionSummary>): DecisionSummary => ({ id: 'i', key: 'DECN-1', topicId: 't', meetingId: null, outcome: 'Approved', status: 'Issued', title: { en: 'd', ar: 'd' }, issuedAt: '2026-06-01', ...p });
const action = (p: Partial<ActionSummary>): ActionSummary => ({ id: 'i', key: 'ACT-1', title: { en: 'a', ar: 'a' }, status: 'Open', priority: 'Normal', ownerUserId: 'u', ownerName: 'o', dueDate: null, isOverdue: false, progressPct: 0, sourceType: 'Topic', sourceId: 's', sourceKey: null, meetingKey: null, ...p });
const risk = (p: Partial<RiskSummary>): RiskSummary => ({ id: 'i', key: 'RSK-1', title: { en: 'r', ar: 'r' }, status: 'Open', likelihood: 'High', impact: 'High', severity: 9, exposure: 'Critical', ownerUserId: '', ownerName: '', subjectType: 'Topic', subjectId: 'i', subjectKey: 'TOP-1', ...p });
const dep = (p: Partial<DependencySummary>): DependencySummary => ({ id: 'i', key: 'DPN-1', fromType: 'Topic', fromId: 'i', fromKey: 'TOP-1', fromTitle: 'a', toType: 'Topic', toId: 't2', toKey: 'TOP-2', toTitle: 'b', kind: 'Blocks', status: 'Open', isBlocker: true, ...p });

const setup = () => render(<MemoryRouter><ReportsPage /></MemoryRouter>);
const loaded = () => {
  mBacklog.mockReturnValue(paged([topic({ streams: ['identity'] }), topic({ key: 'TOP-2', streams: ['payments'] })]));
  mDecisions.mockReturnValue(arr([decision({})]));
  mActions.mockReturnValue(paged([action({})]));
  mRisks.mockReturnValue(paged([risk({})]));
  mDeps.mockReturnValue(paged([dep({})]));
};

describe('ReportsPage (P12-PR3 shell)', () => {
  beforeEach(() => {
    [mBacklog, mDecisions, mActions, mRisks, mDeps].forEach((m) => m.mockReset());
    loaded();
    URL.createObjectURL = vi.fn(() => 'blob:x');
    URL.revokeObjectURL = vi.fn();
  });

  it('renders the six view-tabs and the default executive cards', () => {
    setup();
    expect(screen.getAllByRole('tab')).toHaveLength(6);
    expect(screen.getByRole('heading', { name: 'Decision outcomes' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Risk exposure' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Open items' })).toBeInTheDocument();
    // the per-week throughput card is an honest-empty trend
    expect(screen.getByText('Trend data — Phase 3')).toBeInTheDocument();
  });

  it('switches views and shows that view’s cards (aging columns are REAL)', async () => {
    setup();
    await userEvent.click(screen.getByRole('tab', { name: 'Committee' }));
    expect(screen.getByRole('heading', { name: 'Topic aging' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Backlog by status' })).toBeInTheDocument();
    // attendance is a seam (not on MeetingSummary) → honest-empty
    expect(screen.getByText('Not available yet — Phase 3')).toBeInTheDocument();
  });

  it('offers a real stream filter built from the topics', () => {
    setup();
    const select = screen.getByRole('combobox');
    expect(select).toHaveValue('all');
    expect(screen.getByRole('option', { name: 'identity' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'payments' })).toBeInTheDocument();
  });

  it('narrows the data when a stream is selected', async () => {
    setup();
    await userEvent.click(screen.getByRole('tab', { name: 'Committee' }));
    // both topics active → "2 active topics"
    expect(screen.getByText('2 active topics')).toBeInTheDocument();
    await userEvent.selectOptions(screen.getByRole('combobox'), 'identity');
    // only the identity topic remains
    expect(screen.getByText('1 active topics')).toBeInTheDocument();
  });

  it('shows a last-updated timestamp from the freshest read', () => {
    mBacklog.mockReturnValue({ ...paged([topic({})]), dataUpdatedAt: Date.parse('2026-06-24T09:30:00Z') });
    setup();
    expect(screen.getByText(/Updated/)).toBeInTheDocument();
  });

  it('exports the current view as CSV', async () => {
    const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
    setup();
    await userEvent.click(screen.getByRole('button', { name: /export csv/i }));
    expect(URL.createObjectURL).toHaveBeenCalled();
    expect(clickSpy).toHaveBeenCalled();
    clickSpy.mockRestore();
  });

  it('shows the loading skeleton while any read is pending', () => {
    mBacklog.mockReturnValue({ data: undefined, isLoading: true, isError: false, refetch: vi.fn() });
    const { container } = setup();
    expect(container.querySelector('.rpt-skel')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Open items' })).not.toBeInTheDocument();
  });

  it('shows the error state and refetches on retry', async () => {
    const refetch = vi.fn();
    mRisks.mockReturnValue({ data: undefined, isLoading: false, isError: true, refetch });
    setup();
    expect(screen.getByText('Couldn’t load reports')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /retry/i }));
    expect(refetch).toHaveBeenCalled();
  });

  it('shows the empty state when every register is empty', () => {
    mBacklog.mockReturnValue(paged<TopicSummary>([]));
    mDecisions.mockReturnValue(arr<DecisionSummary>([]));
    mActions.mockReturnValue(paged<ActionSummary>([]));
    mRisks.mockReturnValue(paged<RiskSummary>([]));
    mDeps.mockReturnValue(paged<DependencySummary>([]));
    setup();
    expect(screen.getByText('No reporting data yet')).toBeInTheDocument();
  });

  it('is axe-clean when loaded', async () => {
    const { container } = setup();
    const results = await axe.run(container);
    expect(results.violations).toEqual([]);
  });
});
