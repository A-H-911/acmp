import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import axe from 'axe-core';
import { renderWithAuth } from '../../test/render';
import { DependenciesRegister } from './DependenciesRegister';
import type { DependencySummary } from '../../api/dependencies';

vi.mock('../../api/dependencies', async (orig) => ({
  ...(await orig<typeof import('../../api/dependencies')>()),
  useDependenciesRegister: vi.fn(),
  useDependenciesCounts: vi.fn(),
}));
// The create dialog has its own test; stub it so this test stays isolated from its providers.
vi.mock('./CreateDependencyDialog', () => ({ CreateDependencyDialog: () => null }));
import { useDependenciesRegister, useDependenciesCounts } from '../../api/dependencies';

const mockReg = useDependenciesRegister as unknown as Mock;
const mockCounts = useDependenciesCounts as unknown as Mock;

const ROW = (o: Partial<DependencySummary> = {}): DependencySummary => ({
  id: 'd1', key: 'DPN-2026-001',
  fromType: 'Topic', fromId: 'a', fromKey: 'TOP-2026-014', fromTitle: 'Gateway',
  toType: 'Topic', toId: 'b', toKey: 'TOP-2026-022', toTitle: 'Pagination',
  kind: 'DependsOn', status: 'Open', isBlocker: false, ...o,
});

function regResult(over: Partial<ReturnType<typeof useDependenciesRegister>>) {
  mockReg.mockReturnValue({ data: undefined, isLoading: false, isError: false, refetch: vi.fn(), ...over } as ReturnType<typeof useDependenciesRegister>);
}
function page(items: DependencySummary[]) {
  return { data: { items, total: items.length, page: 1, pageSize: 25, totalPages: 1 } };
}

describe('DependenciesRegister (P10e)', () => {
  beforeEach(() => {
    mockReg.mockReset();
    mockCounts.mockReset();
    mockCounts.mockReturnValue({ total: 7, blocked: 2 });
  });

  it('renders the global counts, a blocked badge, and a row (From · Relation · To · Blocked · Status)', () => {
    regResult(page([ROW({ isBlocker: false }), ROW({ id: 'd2', key: 'DPN-2026-002', fromKey: 'TOP-2026-050', kind: 'Blocks', isBlocker: true, status: 'Resolved' })]));
    renderWithAuth(<DependenciesRegister />, { roles: ['secretary'] });
    expect(screen.getByText('7 links')).toBeInTheDocument();
    expect(screen.getByText('2 blocked')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'TOP-2026-014' })).toHaveAttribute('href', '/dependencies/DPN-2026-001');
    expect(screen.getByText('Depends on')).toBeInTheDocument();
    expect(screen.getByText('Resolved')).toBeInTheDocument();
  });

  it('shows the "New dependency" button for a secretary and a disabled "Open graph" stub', () => {
    regResult(page([ROW()]));
    renderWithAuth(<DependenciesRegister />, { roles: ['secretary'] });
    expect(screen.getByRole('button', { name: /New dependency/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Open graph/ })).toBeDisabled();
  });

  it('hides the "New dependency" button from a plain member', () => {
    regResult(page([ROW()]));
    renderWithAuth(<DependenciesRegister />, { roles: ['member'] });
    expect(screen.queryByRole('button', { name: /New dependency/ })).not.toBeInTheDocument();
  });

  it('toggles the blocked-work filter (aria-pressed)', async () => {
    regResult(page([ROW()]));
    const user = userEvent.setup();
    renderWithAuth(<DependenciesRegister />, { roles: ['member'] });
    const toggle = screen.getByRole('button', { name: /Blocked work/ });
    expect(toggle).toHaveAttribute('aria-pressed', 'false');
    await user.click(toggle);
    expect(toggle).toHaveAttribute('aria-pressed', 'true');
  });

  const lastParams = () => (mockReg.mock.calls.at(-1)![0] ?? {}) as { sortBy?: string; sortDir?: string };

  it('changes the sort column and toggles direction (server-backed sorts only)', async () => {
    regResult(page([ROW()]));
    const user = userEvent.setup();
    renderWithAuth(<DependenciesRegister />, { roles: ['member'] });
    // 'From' maps to sortBy 'key'; default is from/asc → clicking toggles to desc.
    await user.click(screen.getByRole('button', { name: /From/ }));
    expect(lastParams()).toMatchObject({ sortBy: 'key', sortDir: 'desc' });
    // A different column ('Status' → sortBy 'status') resets to asc.
    await user.click(screen.getByRole('button', { name: /Status/ }));
    expect(lastParams()).toMatchObject({ sortBy: 'status', sortDir: 'asc' });
  });

  it('surfaces a Clear filters action in the empty state and clears the active filter', async () => {
    regResult(page([]));
    const user = userEvent.setup();
    renderWithAuth(<DependenciesRegister />, { roles: ['member'] });
    expect(screen.queryByRole('button', { name: 'Clear filters' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Blocked work/ })); // sets a filter → hasFilters
    await user.click(screen.getByRole('button', { name: 'Clear filters' }));
    expect(screen.getByRole('button', { name: /Blocked work/ })).toHaveAttribute('aria-pressed', 'false');
  });

  it('renders the empty state', () => {
    regResult(page([]));
    renderWithAuth(<DependenciesRegister />, { roles: ['member'] });
    expect(screen.getByText('No dependencies match these filters')).toBeInTheDocument();
  });

  it('renders the error state', () => {
    regResult({ isError: true });
    renderWithAuth(<DependenciesRegister />, { roles: ['member'] });
    expect(screen.getByText('Couldn’t load the dependencies')).toBeInTheDocument();
  });

  it('renders the loading skeleton', () => {
    regResult({ isLoading: true });
    renderWithAuth(<DependenciesRegister />, { roles: ['member'] });
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('is axe-clean (WCAG 2.2 AA structure/ARIA)', async () => {
    regResult(page([ROW(), ROW({ id: 'd2', key: 'DPN-2026-002', isBlocker: true })]));
    const { container } = renderWithAuth(<DependenciesRegister />, { roles: ['secretary'] });
    const results = await axe.run(container);
    expect(results.violations).toEqual([]);
  });
});
