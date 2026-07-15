import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import axe from 'axe-core';
import { ResearchRegister } from './ResearchRegister';
import { renderWithAuth } from '../../test/render';
import type { CommitteeRole } from '../../auth/roles';
import type { MissionSummary } from '../../api/research';

vi.mock('../../api/research', async (orig) => ({
  ...(await orig<typeof import('../../api/research')>()),
  useResearchRegister: vi.fn(),
  useResearchCounts: vi.fn(),
}));
// Isolate the register from the create dialog's data hooks — a light stub proves the open wiring.
vi.mock('./CreateMissionDialog', () => ({
  CreateMissionDialog: ({ open }: { open: boolean }) => (open ? <div>create-mission-dialog</div> : null),
}));
import { useResearchRegister, useResearchCounts } from '../../api/research';

const mockList = useResearchRegister as unknown as Mock;
const mockCounts = useResearchCounts as unknown as Mock;

const ROWS: MissionSummary[] = [
  {
    id: 'm1', key: 'RSCH-2026-005', title: { en: 'Evaluate a unified identity provider', ar: 'تقييم موفّر هوية موحّد' },
    status: 'Active', ownerName: 'Noura P', createdAt: '2026-06-02T09:00:00Z', updatedAt: '2026-06-10T09:00:00Z', findingCount: 2, recommendationCount: 3,
  },
  {
    id: 'm2', key: 'RSCH-2026-002', title: { en: 'API gateway consolidation study', ar: 'دراسة دمج البوابات' },
    // updatedAt null → the "Updated" column falls back to createdAt.
    status: 'Completed', ownerName: '', createdAt: '2026-05-20T09:00:00Z', updatedAt: null, findingCount: 4, recommendationCount: 1,
  },
];

function listResult(over: Partial<ReturnType<typeof useResearchRegister>>) {
  return { data: undefined, isLoading: false, isError: false, refetch: vi.fn(), ...over } as ReturnType<typeof useResearchRegister>;
}
function withRows(items: MissionSummary[] = ROWS, total = items.length, totalPages = 1) {
  mockList.mockReturnValue(listResult({ data: { items, total, page: 1, pageSize: 25, totalPages } }));
}
function setup(roles: CommitteeRole[] = ['secretary']) {
  return renderWithAuth(<ResearchRegister />, { roles });
}
function lastParams() {
  return mockList.mock.calls[mockList.mock.calls.length - 1][0];
}

describe('ResearchRegister (P15b)', () => {
  beforeEach(() => {
    mockList.mockReset();
    mockCounts.mockReset();
    mockCounts.mockReturnValue({ total: 5 });
    withRows();
  });

  it('shows the loading skeleton while fetching', () => {
    mockList.mockReturnValue(listResult({ isLoading: true }));
    setup();
    expect(screen.getByRole('status')).toHaveAttribute('aria-busy', 'true');
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('shows a retryable error state on failure', async () => {
    const refetch = vi.fn();
    mockList.mockReturnValue(listResult({ isError: true, refetch }));
    setup();
    await userEvent.click(screen.getByRole('button', { name: /retry/i }));
    expect(refetch).toHaveBeenCalled();
  });

  it('renders the global header count (filter-independent)', () => {
    setup();
    expect(screen.getByText('5 missions')).toBeInTheDocument();
  });

  it('renders rows: title link, key, counts, status chip, lead fallback, showing line', () => {
    setup();
    const link = screen.getByRole('link', { name: 'Evaluate a unified identity provider' });
    expect(link).toHaveAttribute('href', '/research/RSCH-2026-005');
    expect(screen.getByText('RSCH-2026-005')).toBeInTheDocument();
    expect(screen.getByText('Noura P')).toBeInTheDocument();
    expect(screen.getByText('In discovery')).toBeInTheDocument();
    expect(screen.getByText('Concluded')).toBeInTheDocument();
    // Row 2 has no lead → Unassigned fallback.
    expect(screen.getByText('Unassigned')).toBeInTheDocument();
    expect(screen.getByText('Showing 2 of 2')).toBeInTheDocument();
  });

  it('filters by status via the server params', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(within(screen.getByRole('search')).getByRole('button', { name: 'Status' }));
    await user.click(screen.getByText('Cancelled')); // unique — not a status in the mocked rows
    expect(lastParams().statuses).toEqual(['Cancelled']);
  });

  it('changes the sort column and toggles direction (server-backed sorts only)', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: /Mission/ }));
    expect(lastParams()).toMatchObject({ sortBy: 'title', sortDir: 'desc' });
    await user.click(screen.getByRole('button', { name: /Mission/ }));
    expect(lastParams()).toMatchObject({ sortBy: 'title', sortDir: 'asc' });
  });

  it('opens the create dialog from the New mission button (Chair/Sec)', async () => {
    const user = userEvent.setup();
    setup(['chairman']);
    expect(screen.queryByText('create-mission-dialog')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /New mission/ }));
    expect(screen.getByText('create-mission-dialog')).toBeInTheDocument();
  });

  it('hides the New mission button from a Member (API denies non-Chair/Sec create)', () => {
    setup(['member']);
    expect(screen.queryByRole('button', { name: /New mission/ })).not.toBeInTheDocument();
    // But a Member still reads the register.
    expect(screen.getByRole('link', { name: 'Evaluate a unified identity provider' })).toBeInTheDocument();
  });

  it('renders an empty state with a clear button only when a filter is active', async () => {
    const user = userEvent.setup();
    withRows([], 0);
    setup();
    expect(screen.getByText('No research missions yet')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Clear filters' })).not.toBeInTheDocument();
    await user.click(within(screen.getByRole('search')).getByRole('button', { name: 'Status' }));
    await user.click(screen.getByText('Cancelled'));
    await user.click(screen.getByRole('button', { name: 'Clear filters' }));
    expect(lastParams().statuses).toBeUndefined();
  });

  it('is axe-clean (WCAG 2.2 AA structure/ARIA)', async () => {
    setup();
    const results = await axe.run(document.body, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(results.violations.map((v) => v.id)).toEqual([]);
  });
});
