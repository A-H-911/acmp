import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import axe from 'axe-core';
import { RiskPage } from './RiskPage';

// The traceability panel has its own test (TraceabilityPanel.test.tsx); stub it here so this page
// test stays isolated from the panel's auth/query providers.
vi.mock('../traceability/TraceabilityPanel', () => ({ TraceabilityPanel: () => 'TRACE_PANEL' }));
import { ApiError } from '../../api/apiClient';
import type { RiskDetail } from '../../api/risks';

vi.mock('../../api/risks', async (orig) => ({
  ...(await orig<typeof import('../../api/risks')>()),
  useRisk: vi.fn(),
}));
import { useRisk } from '../../api/risks';

const mockRisk = useRisk as unknown as Mock;

const DETAIL: RiskDetail = {
  id: 'r1', key: 'RSK-2026-006', title: { en: 'Dual-running of internal auth and Keycloak', ar: 'التشغيل المزدوج' },
  status: 'Mitigating', likelihood: 'High', impact: 'High', severity: 9, exposure: 'Critical',
  ownerUserId: 'kc-noura', ownerName: 'Noura Q', subjectType: 'Topic', subjectId: 'g1', subjectKey: 'TOP-2026-014',
  description: null,
  mitigations: [
    { id: 'm1', description: { en: 'Stage the cutover stream-by-stream with a tested rollback.', ar: 'نفّذ التبديل مسارًا تلو الآخر.' }, type: 'Reduce', status: 'Planned', ownerUserId: null, linkedActionId: null, dueDate: null },
  ],
  closureNote: null, acceptanceRationale: null, acceptingAuthority: null,
  escalationReason: null, escalationTarget: null, closedAt: null, createdAt: '2026-02-14T00:00:00Z',
};

function result(over: Partial<ReturnType<typeof useRisk>>) {
  mockRisk.mockReturnValue({ data: undefined, isLoading: false, isError: false, error: null, refetch: vi.fn(), ...over } as ReturnType<typeof useRisk>);
}
function setup(path = '/risks/RSK-2026-006') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/risks/:key" element={<RiskPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('RiskPage (P10b)', () => {
  beforeEach(() => mockRisk.mockReset());

  it('shows the loading state while fetching', () => {
    result({ isLoading: true });
    setup();
    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();
  });

  it('shows a not-found empty state on 404', () => {
    result({ isError: true, error: new ApiError(404, undefined) });
    setup();
    expect(screen.getByText('Risk not found')).toBeInTheDocument();
  });

  it('shows a retryable error state on a non-404 failure', async () => {
    const refetch = vi.fn();
    result({ isError: true, error: new ApiError(500, undefined), refetch });
    setup();
    await userEvent.click(screen.getByRole('button', { name: /retry/i }));
    expect(refetch).toHaveBeenCalled();
  });

  it('renders header, facts, exposure matrix, mitigation plan and related from the DTO', () => {
    result({ data: DETAIL });
    setup();
    expect(screen.getByRole('heading', { name: 'Dual-running of internal auth and Keycloak' })).toBeInTheDocument();
    // Key appears twice: the header chip + the Linked-artifact/subject rows.
    expect(screen.getAllByText('RSK-2026-006').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Mitigating').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('Noura Q')).toBeInTheDocument();
    expect(screen.getAllByText('Critical').length).toBeGreaterThanOrEqual(1);
    // Exposure matrix carries an accessible description of the plotted cell.
    expect(screen.getByRole('img', { name: /Probability High, impact High, exposure Critical/ })).toBeInTheDocument();
    // Mitigation plan is a prose card (no mitigations table).
    expect(screen.getByText('Mitigation plan')).toBeInTheDocument();
    expect(screen.getByText('Stage the cutover stream-by-stream with a tested rollback.')).toBeInTheDocument();
    // Related panel shows the linked subject key (display-only) + the trace-pending note.
    expect(screen.getAllByText('TOP-2026-014').length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText('Linked artifact')).toBeInTheDocument();
    expect(screen.getByText(/Full traceability/)).toBeInTheDocument();
  });

  it('omits the mitigation-plan card when the risk has no mitigations', () => {
    result({ data: { ...DETAIL, mitigations: [] } });
    setup();
    expect(screen.queryByText('Mitigation plan')).not.toBeInTheDocument();
  });

  it('omits the related panel when there is no linked subject', () => {
    result({ data: { ...DETAIL, subjectKey: null } });
    setup();
    expect(screen.queryByRole('complementary', { name: 'Related' })).not.toBeInTheDocument();
    expect(screen.getByText('—')).toBeInTheDocument(); // the Linked fact
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
