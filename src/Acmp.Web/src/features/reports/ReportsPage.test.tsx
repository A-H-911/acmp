import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import axe from 'axe-core';
import { ReportsPage } from './ReportsPage';
import type { RiskSummary } from '../../api/risks';
import type { DependencySummary } from '../../api/dependencies';
import type { TopicSummary } from '../../api/topics';

vi.mock('../../api/risks', async (orig) => ({
  ...(await orig<typeof import('../../api/risks')>()),
  useRisksRegister: vi.fn(),
}));
vi.mock('../../api/dependencies', async (orig) => ({
  ...(await orig<typeof import('../../api/dependencies')>()),
  useDependenciesRegister: vi.fn(),
}));
vi.mock('../../api/topics', async (orig) => ({
  ...(await orig<typeof import('../../api/topics')>()),
  useBacklog: vi.fn(),
}));
import { useRisksRegister } from '../../api/risks';
import { useDependenciesRegister } from '../../api/dependencies';
import { useBacklog } from '../../api/topics';

const mockRisks = useRisksRegister as unknown as Mock;
const mockDeps = useDependenciesRegister as unknown as Mock;
const mockTopics = useBacklog as unknown as Mock;

const RISKS: RiskSummary[] = [
  { id: 'r1', key: 'RSK-2026-001', title: { en: 'a', ar: 'a' }, status: 'Open', likelihood: 'High', impact: 'High', severity: 9, exposure: 'Critical', ownerUserId: '', ownerName: '', subjectType: 'Topic', subjectId: 't1', subjectKey: 'TOP-1' },
  { id: 'r2', key: 'RSK-2026-002', title: { en: 'b', ar: 'b' }, status: 'Mitigating', likelihood: 'Low', impact: 'Low', severity: 1, exposure: 'Low', ownerUserId: '', ownerName: '', subjectType: 'Topic', subjectId: 't2', subjectKey: 'TOP-2' },
];
const DEPS: DependencySummary[] = [
  { id: 'd1', key: 'DPN-2026-001', fromType: 'Topic', fromId: 't1', fromKey: 'TOP-1', fromTitle: 'a', toType: 'Topic', toId: 't2', toKey: 'TOP-2', toTitle: 'b', kind: 'BlockedBy', status: 'Open', isBlocker: true },
];
const TOPICS = [
  { id: 't1', key: 'TOP-1', streams: ['identity', 'payments'] },
  { id: 't2', key: 'TOP-2', streams: ['identity'] },
] as unknown as TopicSummary[];

function paged<T>(items: T[]) {
  return { data: { items, total: items.length, page: 1, pageSize: 500, totalPages: 1 }, isLoading: false, isError: false, refetch: vi.fn() };
}
function loaded() {
  mockRisks.mockReturnValue(paged(RISKS));
  mockDeps.mockReturnValue(paged(DEPS));
  mockTopics.mockReturnValue(paged(TOPICS));
}
function setup() {
  return render(<MemoryRouter><ReportsPage /></MemoryRouter>);
}

describe('ReportsPage (P10g)', () => {
  beforeEach(() => {
    mockRisks.mockReset();
    mockDeps.mockReset();
    mockTopics.mockReset();
  });

  it('renders all six risk/dependency cards when loaded', () => {
    loaded();
    setup();
    expect(screen.getByRole('heading', { name: 'Risk exposure' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Risk overview' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Dependency overview' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Dependencies by relation' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Risk by stream' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Cross-stream dependencies' })).toBeInTheDocument();
  });

  it('shows the active-risk matrix count and a stream code bar', () => {
    loaded();
    setup();
    expect(screen.getByText(/2 active/)).toBeInTheDocument();
    // by-stream cards render raw stream codes (no localized name on the wire)
    expect(screen.getAllByText('identity').length).toBeGreaterThan(0);
  });

  it('renders the deferred-scope P12 note', () => {
    loaded();
    setup();
    expect(screen.getByRole('note')).toHaveTextContent(/Reporting module/);
  });

  it('shows the loading skeleton while any read is pending', () => {
    mockRisks.mockReturnValue({ data: undefined, isLoading: true, isError: false, refetch: vi.fn() });
    mockDeps.mockReturnValue(paged(DEPS));
    mockTopics.mockReturnValue(paged(TOPICS));
    const { container } = setup();
    expect(container.querySelector('.rpt-skel')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Risk exposure' })).not.toBeInTheDocument();
  });

  it('shows an error state and refetches all three reads on retry', async () => {
    const refetch = vi.fn();
    const depsRefetch = vi.fn();
    const topicsRefetch = vi.fn();
    mockRisks.mockReturnValue({ data: undefined, isLoading: false, isError: true, refetch });
    mockDeps.mockReturnValue({ ...paged(DEPS), refetch: depsRefetch });
    mockTopics.mockReturnValue({ ...paged(TOPICS), refetch: topicsRefetch });
    setup();
    expect(screen.getByText('Couldn’t load reports')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /retry/i }));
    expect(refetch).toHaveBeenCalled();
    expect(depsRefetch).toHaveBeenCalled();
    expect(topicsRefetch).toHaveBeenCalled();
  });

  it('shows the empty state when both registers are empty', () => {
    mockRisks.mockReturnValue(paged<RiskSummary>([]));
    mockDeps.mockReturnValue(paged<DependencySummary>([]));
    mockTopics.mockReturnValue(paged(TOPICS));
    setup();
    expect(screen.getByText('No reporting data yet')).toBeInTheDocument();
  });

  it('is axe-clean when loaded', async () => {
    loaded();
    const { container } = setup();
    const results = await axe.run(container);
    expect(results.violations).toEqual([]);
  });
});
