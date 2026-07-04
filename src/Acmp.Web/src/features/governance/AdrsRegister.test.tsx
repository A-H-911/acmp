import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import axe from 'axe-core';
import { AdrsRegister } from './AdrsRegister';
import type { AdrSummary } from '../../api/adrs';

vi.mock('../../api/adrs', async (orig) => ({
  ...(await orig<typeof import('../../api/adrs')>()),
  useAdrsRegister: vi.fn(),
  useAdrsCount: vi.fn(),
}));
// Isolate the register from the create dialog's data hooks — a light stub proves the open wiring.
vi.mock('./CreateAdrDialog', () => ({
  CreateAdrDialog: ({ open }: { open: boolean }) => (open ? <div>create-adr-dialog</div> : null),
}));
import { useAdrsRegister, useAdrsCount } from '../../api/adrs';

const mockList = useAdrsRegister as unknown as Mock;
const mockCount = useAdrsCount as unknown as Mock;

const ROWS: AdrSummary[] = [
  { id: 'a1', key: 'ADR-2026-003', title: { en: 'Keycloak as the standard IdP', ar: 'كيكلوك' }, status: 'Proposed', authorName: 'Khalid A', approvedAt: null, createdAt: '2026-02-18T00:00:00Z', isSuperseded: false },
  { id: 'a2', key: 'ADR-2025-019', title: { en: 'Cursor pagination (interim)', ar: 'الترقيم بالمؤشّر' }, status: 'Superseded', authorName: 'Noura P', approvedAt: '2025-11-01T00:00:00Z', createdAt: '2025-11-01T00:00:00Z', isSuperseded: true },
];

function listResult(over: Partial<ReturnType<typeof useAdrsRegister>>) {
  return { data: undefined, isLoading: false, isError: false, refetch: vi.fn(), ...over } as ReturnType<typeof useAdrsRegister>;
}
function withRows(items: AdrSummary[] = ROWS, total = items.length, totalPages = 1) {
  mockList.mockReturnValue(listResult({ data: { items, total, page: 1, pageSize: 25, totalPages } }));
}
function setup() {
  return render(<MemoryRouter><AdrsRegister /></MemoryRouter>);
}
function lastParams() {
  return mockList.mock.calls[mockList.mock.calls.length - 1][0];
}

describe('AdrsRegister (P11b)', () => {
  beforeEach(() => {
    mockList.mockReset();
    mockCount.mockReset();
    mockCount.mockReturnValue({ data: 7 });
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

  it('renders the filter-independent header total and the tab bar (ADRs active, Invariants links to /invariants)', () => {
    setup();
    expect(screen.getByText('7 records')).toBeInTheDocument();
    const adrsTab = screen.getByRole('tab', { name: /ADRs/ });
    expect(adrsTab).toHaveAttribute('aria-selected', 'true');
    const invTab = screen.getByRole('tab', { name: /Architecture Invariants/ });
    expect(invTab).toHaveAttribute('aria-selected', 'false');
    expect(invTab).toHaveAttribute('href', '/invariants');
  });

  it('renders rows: key, title link, status chip, and a superseded marker only for superseded rows', () => {
    setup();
    expect(screen.getByText('ADR-2026-003')).toBeInTheDocument();
    const link = screen.getByRole('link', { name: 'Keycloak as the standard IdP' });
    expect(link).toHaveAttribute('href', '/adrs/ADR-2026-003');
    expect(screen.getByText('Proposed')).toBeInTheDocument();
    // The unsuperseded row shows a dash; the superseded row shows the marker chip.
    expect(screen.getByText('—')).toBeInTheDocument();
    expect(screen.getAllByText('Superseded').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('Showing 2 of 2')).toBeInTheDocument();
  });

  it('filters by status via the server params', async () => {
    const user = userEvent.setup();
    setup();
    // Disambiguate the Status FILTER chip from the sortable Status column header.
    await user.click(within(screen.getByRole('search')).getByRole('button', { name: 'Status' }));
    await user.click(screen.getByText('Deprecated')); // unique — not a status in the mocked rows
    expect(lastParams().statuses).toEqual(['Deprecated']);
  });

  it('changes the sort column and toggles direction (server-backed sorts only)', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: /ID/ }));
    expect(lastParams()).toMatchObject({ sortBy: 'key', sortDir: 'desc' });
    await user.click(screen.getByRole('button', { name: /ID/ }));
    expect(lastParams()).toMatchObject({ sortBy: 'key', sortDir: 'asc' });
  });

  it('opens the create dialog from the New ADR button', async () => {
    const user = userEvent.setup();
    setup();
    expect(screen.queryByText('create-adr-dialog')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /New ADR/ }));
    expect(screen.getByText('create-adr-dialog')).toBeInTheDocument();
  });

  it('renders an empty state with a clear button only when a filter is active', async () => {
    const user = userEvent.setup();
    withRows([], 0);
    setup();
    expect(screen.getByText('No ADRs match these filters')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Clear filters' })).not.toBeInTheDocument();
    await user.click(within(screen.getByRole('search')).getByRole('button', { name: 'Status' }));
    await user.click(screen.getByText('Deprecated'));
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
