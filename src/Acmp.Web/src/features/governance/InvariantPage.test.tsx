import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import axe from 'axe-core';
import { InvariantPage } from './InvariantPage';

// The traceability panel has its own test; stub it so this page test stays isolated from its providers.
vi.mock('../traceability/TraceabilityPanel', () => ({ TraceabilityPanel: () => 'TRACE_PANEL' }));
import { ApiError } from '../../api/apiClient';
import type { InvariantDetail } from '../../api/invariants';

vi.mock('../../api/invariants', async (orig) => ({
  ...(await orig<typeof import('../../api/invariants')>()),
  useInvariant: vi.fn(),
}));
import { useInvariant } from '../../api/invariants';

const mockInv = useInvariant as unknown as Mock;

const DETAIL: InvariantDetail = {
  id: 'i1', key: 'AIV-2026-003', status: 'Active', category: 'Security', scope: 'Platform',
  statement: { en: 'Every service authenticates via the standard IdP', ar: 'كل خدمة تُصادَق عبر موفّر الهوية' },
  rationale: { en: 'Centralized identity is auditable and revocable.', ar: 'الهوية المركزية قابلة للتدقيق.' },
  exceptionsPolicy: { en: 'Legacy batch jobs are exempt until Q3.', ar: 'المهام الدُفعية القديمة مُعفاة حتى الربع الثالث.' },
  ownerUserId: 'kc-1', ownerName: 'Khalid A', activatedAt: '2026-02-18T00:00:00Z', activatedByName: 'S. M.',
  supersededByInvariantId: 'i9', supersededByInvariantKey: 'AIV-2026-012', supersessionReason: null,
  supersedesInvariantId: 'i0', supersedesInvariantKey: 'AIV-2025-018', retirementReason: null,
  createdAt: '2026-02-10T00:00:00Z',
};

function result(over: Partial<ReturnType<typeof useInvariant>>) {
  mockInv.mockReturnValue({ data: undefined, isLoading: false, isError: false, error: null, refetch: vi.fn(), ...over } as ReturnType<typeof useInvariant>);
}
function setup(path = '/invariants/AIV-2026-003') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/invariants/:key" element={<InvariantPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('InvariantPage (P11d)', () => {
  beforeEach(() => {
    mockInv.mockReset();
  });

  it('shows the loading state while fetching', () => {
    result({ isLoading: true });
    setup();
    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();
  });

  it('shows a not-found empty state on 404', () => {
    result({ isError: true, error: new ApiError(404, undefined) });
    setup();
    expect(screen.getByText('Invariant not found')).toBeInTheDocument();
  });

  it('shows a retryable error state on a non-404 failure', async () => {
    const refetch = vi.fn();
    result({ isError: true, error: new ApiError(500, undefined), refetch });
    setup();
    await userEvent.click(screen.getByRole('button', { name: /retry/i }));
    expect(refetch).toHaveBeenCalled();
  });

  it('renders the statement heading, rationale, exceptions, metadata facts and supersede links', () => {
    result({ data: DETAIL });
    setup();
    expect(screen.getByRole('heading', { name: 'Every service authenticates via the standard IdP' })).toBeInTheDocument();
    expect(screen.getByText('AIV-2026-003')).toBeInTheDocument();
    // Body sections.
    expect(screen.getByText('Rationale')).toBeInTheDocument();
    expect(screen.getByText('Centralized identity is auditable and revocable.')).toBeInTheDocument();
    expect(screen.getByText('Exceptions policy')).toBeInTheDocument();
    expect(screen.getByText('Legacy batch jobs are exempt until Q3.')).toBeInTheDocument();
    // Metadata facts (category / scope / owner).
    expect(screen.getByText('Security')).toBeInTheDocument();
    expect(screen.getByText('Platform')).toBeInTheDocument();
    expect(screen.getByText('Khalid A')).toBeInTheDocument();
    // Supersede banner both directions.
    expect(screen.getByText('AIV-2025-018')).toBeInTheDocument();
    expect(screen.getByText('AIV-2026-012')).toBeInTheDocument();
    // Read-only: no lifecycle buttons this slice, and no export (invariants have no markdown).
    expect(screen.queryByRole('button', { name: /Propose|Activate|Retire|Supersede|Export/ })).not.toBeInTheDocument();
  });

  it('omits the exceptions section and the supersede banner when absent', () => {
    result({
      data: { ...DETAIL, status: 'Draft', exceptionsPolicy: null, supersedesInvariantKey: null, supersededByInvariantKey: null },
    });
    setup();
    expect(screen.queryByText('Exceptions policy')).not.toBeInTheDocument();
    expect(screen.queryByText('Supersedes')).not.toBeInTheDocument();
    // Rationale (required) still renders.
    expect(screen.getByText('Rationale')).toBeInTheDocument();
  });

  it('falls back to Unassigned when no owner name is set', () => {
    result({ data: { ...DETAIL, ownerName: '' } });
    setup();
    expect(screen.getByText('Unassigned')).toBeInTheDocument();
  });

  it('is axe-clean (WCAG 2.2 AA structure/ARIA)', async () => {
    result({ data: DETAIL });
    setup();
    const results = await axe.run(document.body, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(results.violations.map((v) => v.id)).toEqual([]);
  });
});
