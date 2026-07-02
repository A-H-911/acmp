import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import axe from 'axe-core';
import { RisksRegister } from './RisksRegister';
import type { RiskSummary } from '../../api/risks';

vi.mock('../../api/risks', async (orig) => ({
  ...(await orig<typeof import('../../api/risks')>()),
  useRisksRegister: vi.fn(),
  useRisksCounts: vi.fn(),
}));
// Isolate the register from the create dialog's data hooks — a light stub proves the open wiring.
vi.mock('./CreateRiskDialog', () => ({
  CreateRiskDialog: ({ open }: { open: boolean }) => (open ? <div>create-risk-dialog</div> : null),
}));
import { useRisksRegister, useRisksCounts } from '../../api/risks';

const mockList = useRisksRegister as unknown as Mock;
const mockCounts = useRisksCounts as unknown as Mock;

const ROWS: RiskSummary[] = [
  {
    id: 'r1', key: 'RSK-2026-006', title: { en: 'Dual-running of internal auth and Keycloak', ar: 'التشغيل المزدوج' },
    status: 'Open', likelihood: 'High', impact: 'High', severity: 9, exposure: 'Critical',
    ownerUserId: 'kc-noura', ownerName: 'Noura Q', subjectType: 'Topic', subjectId: 'g1', subjectKey: 'TOP-2026-014',
  },
  {
    id: 'r2', key: 'RSK-2026-004', title: { en: 'Token policy gaps during migration', ar: 'ثغرات سياسة الرموز' },
    status: 'Mitigating', likelihood: 'Medium', impact: 'High', severity: 6, exposure: 'High',
    ownerUserId: '', ownerName: '', subjectType: 'Decision', subjectId: 'g2', subjectKey: null,
  },
];

function listResult(over: Partial<ReturnType<typeof useRisksRegister>>) {
  return { data: undefined, isLoading: false, isError: false, refetch: vi.fn(), ...over } as ReturnType<typeof useRisksRegister>;
}
function withRows(items: RiskSummary[] = ROWS, total = items.length, totalPages = 1) {
  mockList.mockReturnValue(listResult({ data: { items, total, page: 1, pageSize: 25, totalPages } }));
}
function setup() {
  return render(<MemoryRouter><RisksRegister /></MemoryRouter>);
}
function lastParams() {
  return mockList.mock.calls[mockList.mock.calls.length - 1][0];
}

describe('RisksRegister (P10b)', () => {
  beforeEach(() => {
    mockList.mockReset();
    mockCounts.mockReset();
    mockCounts.mockReturnValue({ total: 5, critical: 2 });
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

  it('renders the global header counts (filter-independent)', () => {
    setup();
    expect(screen.getByText('5 risks')).toBeInTheDocument();
    expect(screen.getByText('2 critical')).toBeInTheDocument();
  });

  it('hides the critical count badge when there are none', () => {
    mockCounts.mockReturnValue({ total: 5, critical: 0 });
    setup();
    expect(screen.queryByText(/\d+ critical/)).not.toBeInTheDocument();
  });

  it('renders rows: key, title link, level words, exposure chip, owner, status, linked', () => {
    setup();
    expect(screen.getByText('RSK-2026-006')).toBeInTheDocument();
    const link = screen.getByRole('link', { name: 'Dual-running of internal auth and Keycloak' });
    expect(link).toHaveAttribute('href', '/risks/RSK-2026-006');
    expect(screen.getByText('Noura Q')).toBeInTheDocument();
    expect(screen.getByText('Critical')).toBeInTheDocument();
    expect(screen.getByText('Open')).toBeInTheDocument();
    expect(screen.getByText('TOP-2026-014')).toBeInTheDocument();
    // Row 2 falls back: unassigned owner + em dash for the missing linked subject.
    expect(screen.getByText('Unassigned')).toBeInTheDocument();
    expect(screen.getByText('—')).toBeInTheDocument();
    expect(screen.getByText('Showing 2 of 2')).toBeInTheDocument();
  });

  it('filters by status via the server params', async () => {
    const user = userEvent.setup();
    setup();
    // Disambiguate the Status FILTER chip from the sortable Status column header.
    await user.click(within(screen.getByRole('search')).getByRole('button', { name: 'Status' }));
    await user.click(screen.getByText('Accepted')); // unique — not a status in the mocked rows
    expect(lastParams().statuses).toEqual(['Accepted']);
  });

  it('filters by exposure via the server params', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(within(screen.getByRole('search')).getByRole('button', { name: 'Exposure' }));
    await user.click(screen.getByText('Low')); // unique — not an exposure/level in the mocked rows
    expect(lastParams().exposures).toEqual(['Low']);
  });

  it('changes the sort column and toggles direction (server-backed sorts only)', async () => {
    const user = userEvent.setup();
    setup();
    // 'ID' header (key) is server-sortable and collision-free (no ID filter).
    await user.click(screen.getByRole('button', { name: /ID/ }));
    expect(lastParams()).toMatchObject({ sortBy: 'key', sortDir: 'desc' });
    await user.click(screen.getByRole('button', { name: /ID/ }));
    expect(lastParams()).toMatchObject({ sortBy: 'key', sortDir: 'asc' });
  });

  it('opens the create dialog from the New risk button', async () => {
    const user = userEvent.setup();
    setup();
    expect(screen.queryByText('create-risk-dialog')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /New risk/ }));
    expect(screen.getByText('create-risk-dialog')).toBeInTheDocument();
  });

  it('renders an empty state with a clear button only when a filter is active', async () => {
    const user = userEvent.setup();
    withRows([], 0);
    setup();
    expect(screen.getByText('No risks match these filters')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Clear filters' })).not.toBeInTheDocument();
    await user.click(within(screen.getByRole('search')).getByRole('button', { name: 'Exposure' }));
    await user.click(screen.getByText('Low'));
    await user.click(screen.getByRole('button', { name: 'Clear filters' }));
    expect(lastParams().exposures).toBeUndefined();
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
