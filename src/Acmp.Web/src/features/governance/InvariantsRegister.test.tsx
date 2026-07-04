import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import axe from 'axe-core';
import { InvariantsRegister } from './InvariantsRegister';
import type { InvariantSummary } from '../../api/invariants';

vi.mock('../../api/invariants', async (orig) => ({
  ...(await orig<typeof import('../../api/invariants')>()),
  useInvariantsRegister: vi.fn(),
  useInvariantsCount: vi.fn(),
}));
// Isolate the register from the create dialog's data hooks — a light stub proves the open wiring.
vi.mock('./CreateInvariantDialog', () => ({
  CreateInvariantDialog: ({ open }: { open: boolean }) => (open ? <div>create-invariant-dialog</div> : null),
}));
import { useInvariantsRegister, useInvariantsCount } from '../../api/invariants';

const mockList = useInvariantsRegister as unknown as Mock;
const mockCount = useInvariantsCount as unknown as Mock;

const ROWS: InvariantSummary[] = [
  { id: 'i1', key: 'AIV-2026-003', statement: { en: 'Every service authenticates via the standard IdP', ar: 'كل خدمة تُصادَق عبر موفّر الهوية' }, status: 'Active', category: 'Security', scope: 'Platform', ownerName: 'Khalid A', activatedAt: '2026-02-18T00:00:00Z', createdAt: '2026-02-10T00:00:00Z', isSuperseded: false },
  { id: 'i2', key: 'AIV-2025-019', statement: { en: 'No shared mutable schema across streams', ar: 'لا مخطّط مشترك قابل للتغيير' }, status: 'Superseded', category: 'Data', scope: 'MultiStream', ownerName: 'Noura P', activatedAt: null, createdAt: '2025-11-01T00:00:00Z', isSuperseded: true },
];

function listResult(over: Partial<ReturnType<typeof useInvariantsRegister>>) {
  return { data: undefined, isLoading: false, isError: false, refetch: vi.fn(), ...over } as ReturnType<typeof useInvariantsRegister>;
}
function withRows(items: InvariantSummary[] = ROWS, total = items.length, totalPages = 1) {
  mockList.mockReturnValue(listResult({ data: { items, total, page: 1, pageSize: 25, totalPages } }));
}
function setup() {
  return render(<MemoryRouter><InvariantsRegister /></MemoryRouter>);
}
function lastParams() {
  return mockList.mock.calls[mockList.mock.calls.length - 1][0];
}

describe('InvariantsRegister (P11d)', () => {
  beforeEach(() => {
    mockList.mockReset();
    mockCount.mockReset();
    mockCount.mockReturnValue({ data: 9 });
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

  it('renders the filter-independent header total and the tab bar (Invariants active, ADRs links to /adrs)', () => {
    setup();
    expect(screen.getByText('9 invariants')).toBeInTheDocument();
    const invTab = screen.getByRole('tab', { name: /Architecture Invariants/ });
    expect(invTab).toHaveAttribute('aria-selected', 'true');
    const adrsTab = screen.getByRole('tab', { name: /ADRs/ });
    expect(adrsTab).toHaveAttribute('aria-selected', 'false');
    expect(adrsTab).toHaveAttribute('href', '/adrs');
  });

  it('renders rows: key, statement link, category, scope, and status chip', () => {
    setup();
    expect(screen.getByText('AIV-2026-003')).toBeInTheDocument();
    const link = screen.getByRole('link', { name: 'Every service authenticates via the standard IdP' });
    expect(link).toHaveAttribute('href', '/invariants/AIV-2026-003');
    expect(screen.getByText('Security')).toBeInTheDocument();
    expect(screen.getByText('Platform')).toBeInTheDocument();
    expect(screen.getByText('Active')).toBeInTheDocument();
    expect(screen.getByText('Showing 2 of 2')).toBeInTheDocument();
  });

  it('filters by status via the server params', async () => {
    const user = userEvent.setup();
    setup();
    // Disambiguate the Status FILTER chip from the sortable Status column header.
    await user.click(within(screen.getByRole('search')).getByRole('button', { name: 'Status' }));
    await user.click(screen.getByText('Retired')); // unique — not a status in the mocked rows
    expect(lastParams().statuses).toEqual(['Retired']);
  });

  it('changes the sort column and toggles direction (server-backed sorts only)', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: /ID/ }));
    expect(lastParams()).toMatchObject({ sortBy: 'key', sortDir: 'desc' });
    await user.click(screen.getByRole('button', { name: /ID/ }));
    expect(lastParams()).toMatchObject({ sortBy: 'key', sortDir: 'asc' });
    // Statement is server-sortable too (unambiguous — there is no Statement filter chip).
    await user.click(screen.getByRole('button', { name: /Statement/ }));
    expect(lastParams()).toMatchObject({ sortBy: 'statement', sortDir: 'desc' });
  });

  it('opens the create dialog from the New invariant button', async () => {
    const user = userEvent.setup();
    setup();
    expect(screen.queryByText('create-invariant-dialog')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /New invariant/ }));
    expect(screen.getByText('create-invariant-dialog')).toBeInTheDocument();
  });

  it('renders an empty state with a clear button only when a filter is active', async () => {
    const user = userEvent.setup();
    withRows([], 0);
    setup();
    expect(screen.getByText('No invariants match these filters')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Clear filters' })).not.toBeInTheDocument();
    await user.click(within(screen.getByRole('search')).getByRole('button', { name: 'Status' }));
    await user.click(screen.getByText('Retired'));
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
