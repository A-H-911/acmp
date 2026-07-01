import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import axe from 'axe-core';
import { ActionsRegister } from './ActionsRegister';
import type { ActionSummary } from '../../api/actions';

vi.mock('../../api/actions', async (orig) => ({
  ...(await orig<typeof import('../../api/actions')>()),
  useActionsRegister: vi.fn(),
  useActionsCounts: vi.fn(),
}));
import { useActionsRegister, useActionsCounts } from '../../api/actions';

const mockList = useActionsRegister as unknown as Mock;
const mockCounts = useActionsCounts as unknown as Mock;

const ROWS: ActionSummary[] = [
  {
    id: 'a1', key: 'ACT-2026-033', title: { en: 'Risk review: dual-running auth', ar: 'مراجعة مخاطر التشغيل المزدوج' },
    status: 'Blocked', priority: 'High', ownerUserId: 'kc-noura', ownerName: 'Noura Q',
    dueDate: '2026-06-23T00:00:00Z', isOverdue: true, progressPct: 20,
    sourceType: 'Risk', sourceId: 'g1', sourceKey: 'RSK-2026-006', meetingKey: null,
  },
  {
    id: 'a2', key: 'ACT-2026-044', title: { en: 'Schedule cross-stream dependency review', ar: 'جدولة مراجعة الاعتماديات' },
    status: 'Open', priority: 'Normal', ownerUserId: '', ownerName: '',
    dueDate: null, isOverdue: false, progressPct: 0,
    sourceType: 'Topic', sourceId: 'g2', sourceKey: null, meetingKey: null,
  },
];

function listResult(over: Partial<ReturnType<typeof useActionsRegister>>) {
  return { data: undefined, isLoading: false, isError: false, refetch: vi.fn(), ...over } as ReturnType<typeof useActionsRegister>;
}
function withRows(items: ActionSummary[] = ROWS, total = items.length, totalPages = 1) {
  mockList.mockReturnValue(listResult({ data: { items, total, page: 1, pageSize: 25, totalPages } }));
}
function setup() {
  return render(<MemoryRouter><ActionsRegister /></MemoryRouter>);
}
function lastParams() {
  return mockList.mock.calls[mockList.mock.calls.length - 1][0];
}

describe('ActionsRegister (P8b)', () => {
  beforeEach(() => {
    mockList.mockReset();
    mockCounts.mockReset();
    mockCounts.mockReturnValue({ total: 8, overdue: 2 });
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
    const retry = screen.getByRole('button', { name: /retry/i });
    await userEvent.click(retry);
    expect(refetch).toHaveBeenCalled();
  });

  it('renders the global header counts (filter-independent)', () => {
    setup();
    expect(screen.getByText('8 actions')).toBeInTheDocument();
    expect(screen.getByText('2 overdue')).toBeInTheDocument();
  });

  it('hides the overdue count badge when there are none', () => {
    mockCounts.mockReturnValue({ total: 8, overdue: 0 });
    setup();
    // The "N overdue" header badge is gone; the Overdue filter toggle still reads "Overdue".
    expect(screen.queryByText(/\d+ overdue/)).not.toBeInTheDocument();
  });

  it('renders rows: key, title link, owner, due, progress and status', () => {
    setup();
    expect(screen.getByText('ACT-2026-033')).toBeInTheDocument();
    const link = screen.getByRole('link', { name: 'Risk review: dual-running auth' });
    expect(link).toHaveAttribute('href', '/actions/ACT-2026-033');
    expect(screen.getByText('Noura Q')).toBeInTheDocument();
    expect(screen.getByText('RSK-2026-006')).toBeInTheDocument();
    expect(screen.getByText('20%')).toBeInTheDocument();
    expect(screen.getByText('Blocked')).toBeInTheDocument();
    // Row 2 falls back: unassigned owner + em dash for the missing due + '—' linked.
    expect(screen.getByText('Unassigned')).toBeInTheDocument();
    expect(screen.getAllByText('—').length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText('Showing 2 of 2')).toBeInTheDocument();
  });

  it('marks the overdue row for assistive tech', () => {
    setup();
    expect(screen.getByText(/^— Overdue$/)).toBeInTheDocument();
  });

  it('filters by status via the server params', async () => {
    const user = userEvent.setup();
    setup();
    // Disambiguate the Status FILTER chip from the sortable Status column header.
    await user.click(within(screen.getByRole('search')).getByRole('button', { name: 'Status' }));
    await user.click(screen.getByText('Completed')); // unique — not a status in the mocked rows
    expect(lastParams().statuses).toEqual(['Completed']);
  });

  it('toggles the overdue server filter', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: /Overdue/ }));
    expect(lastParams().overdue).toBe(true);
  });

  it('changes the sort column and toggles direction (server-backed sorts only)', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: /Progress/ }));
    expect(lastParams()).toMatchObject({ sortBy: 'progress', sortDir: 'asc' });
    await user.click(screen.getByRole('button', { name: /Due/ }));
    expect(lastParams()).toMatchObject({ sortBy: 'due', sortDir: 'asc' });
  });

  it('renders an empty state with no clear button until a filter is active', async () => {
    const user = userEvent.setup();
    withRows([], 0);
    setup();
    expect(screen.getByText('No actions match these filters')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Clear filters' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Overdue/ }));
    await user.click(screen.getByRole('button', { name: 'Clear filters' }));
    expect(lastParams().overdue).toBeUndefined();
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
