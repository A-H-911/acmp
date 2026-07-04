import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import axe from 'axe-core';
import { AdrPage } from './AdrPage';

// The traceability panel has its own test; stub it so this page test stays isolated from its providers.
vi.mock('../traceability/TraceabilityPanel', () => ({ TraceabilityPanel: () => 'TRACE_PANEL' }));
// Keep statusTone + exportMarkdown real; only spy the browser download side effect.
const download = vi.hoisted(() => vi.fn());
vi.mock('./adrMeta', async (orig) => ({
  ...(await orig<typeof import('./adrMeta')>()),
  downloadMarkdown: download,
}));
import { ApiError } from '../../api/apiClient';
import type { AdrDetail } from '../../api/adrs';

vi.mock('../../api/adrs', async (orig) => ({
  ...(await orig<typeof import('../../api/adrs')>()),
  useAdr: vi.fn(),
}));
import { useAdr } from '../../api/adrs';

const mockAdr = useAdr as unknown as Mock;

const DETAIL: AdrDetail = {
  id: 'a1', key: 'ADR-2026-003', title: { en: 'Keycloak as the standard IdP', ar: 'كيكلوك' },
  status: 'Approved', context: { en: 'Fragmented auth across streams.', ar: 'مصادقة مجزأة.' },
  decisionDrivers: { en: 'Security consolidation.', ar: 'توحيد الأمن.' },
  decisionText: { en: 'Adopt Keycloak, realm per stream.', ar: 'اعتماد كيكلوك.' },
  consequencesPositive: { en: 'Unified SSO and one audit surface.', ar: 'دخول موحّد.' },
  consequencesNegative: { en: 'Migration effort and dual-running risk.', ar: 'جهد الترحيل.' },
  options: [
    { id: 'o1', name: { en: 'Keycloak', ar: 'كيكلوك' }, body: { en: 'Mature OSS IdP.', ar: 'مفتوح المصدر.' }, isChosen: true },
    { id: 'o2', name: { en: 'In-house IdP', ar: 'داخلي' }, body: null, isChosen: false },
  ],
  authorUserId: 'kc-1', authorName: 'Khalid A', sourceDecisionId: null,
  approvedAt: '2026-02-18T00:00:00Z', approvedByName: 'S. M.',
  supersededByAdrId: 'a9', supersededByAdrKey: 'ADR-2026-012', supersessionReason: null,
  supersedesAdrId: 'a0', supersedesAdrKey: 'ADR-2025-018', deprecationReason: null,
  createdAt: '2026-02-10T00:00:00Z',
};

function result(over: Partial<ReturnType<typeof useAdr>>) {
  mockAdr.mockReturnValue({ data: undefined, isLoading: false, isError: false, error: null, refetch: vi.fn(), ...over } as ReturnType<typeof useAdr>);
}
function setup(path = '/adrs/ADR-2026-003') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/adrs/:key" element={<AdrPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('AdrPage (P11b)', () => {
  beforeEach(() => {
    mockAdr.mockReset();
    download.mockReset();
  });

  it('shows the loading state while fetching', () => {
    result({ isLoading: true });
    setup();
    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();
  });

  it('shows a not-found empty state on 404', () => {
    result({ isError: true, error: new ApiError(404, undefined) });
    setup();
    expect(screen.getByText('ADR not found')).toBeInTheDocument();
  });

  it('shows a retryable error state on a non-404 failure', async () => {
    const refetch = vi.fn();
    result({ isError: true, error: new ApiError(500, undefined), refetch });
    setup();
    await userEvent.click(screen.getByRole('button', { name: /retry/i }));
    expect(refetch).toHaveBeenCalled();
  });

  it('renders the MADR body, options (chosen/alternative), consequences, metadata and supersede links', () => {
    result({ data: DETAIL });
    setup();
    expect(screen.getByRole('heading', { name: 'Keycloak as the standard IdP' })).toBeInTheDocument();
    expect(screen.getByText('ADR-2026-003')).toBeInTheDocument();
    // MADR sections.
    expect(screen.getByText('Context & problem statement')).toBeInTheDocument();
    expect(screen.getByText('Decision drivers')).toBeInTheDocument();
    expect(screen.getByText('Considered options')).toBeInTheDocument();
    expect(screen.getByText('Decision outcome')).toBeInTheDocument();
    // Chosen vs alternative option tags.
    expect(screen.getByText('Chosen')).toBeInTheDocument();
    expect(screen.getByText('Alternative')).toBeInTheDocument();
    // Consequences split.
    expect(screen.getByText('Unified SSO and one audit surface.')).toBeInTheDocument();
    expect(screen.getByText('Migration effort and dual-running risk.')).toBeInTheDocument();
    // Metadata aside (Approved-by from the DTO).
    expect(screen.getByText('S. M.')).toBeInTheDocument();
    // Supersede banner both directions.
    expect(screen.getByText('ADR-2025-018')).toBeInTheDocument();
    expect(screen.getByText('ADR-2026-012')).toBeInTheDocument();
    // Read-only: no lifecycle buttons this slice.
    expect(screen.queryByRole('button', { name: /Propose|Approve|Supersede|Deprecate/ })).not.toBeInTheDocument();
  });

  it('exports the ADR to markdown via the Export .md button', async () => {
    result({ data: DETAIL });
    setup();
    await userEvent.click(screen.getByRole('button', { name: /Export \.md/ }));
    expect(download).toHaveBeenCalledOnce();
    expect(download.mock.calls[0][0]).toBe('ADR-2026-003.md');
    expect(download.mock.calls[0][1]).toContain('# ADR-2026-003 — Keycloak as the standard IdP');
  });

  it('omits optional sections and the supersede banner for a lean Draft', () => {
    result({
      data: {
        ...DETAIL, status: 'Draft', decisionDrivers: null, options: [],
        consequencesPositive: null, consequencesNegative: null,
        supersedesAdrKey: null, supersededByAdrKey: null, approvedByName: null,
      },
    });
    setup();
    expect(screen.queryByText('Decision drivers')).not.toBeInTheDocument();
    expect(screen.queryByText('Considered options')).not.toBeInTheDocument();
    expect(screen.queryByText('Consequences')).not.toBeInTheDocument();
    expect(screen.queryByText('Supersedes')).not.toBeInTheDocument();
    // Context + Decision (required) still render.
    expect(screen.getByText('Context & problem statement')).toBeInTheDocument();
    expect(screen.getByText('Decision outcome')).toBeInTheDocument();
  });

  it('dims the body for a superseded/deprecated ADR (the retired treatment)', () => {
    result({ data: { ...DETAIL, status: 'Superseded' } });
    const { container } = setup();
    expect(container.querySelector('.adr-body')).toHaveClass('adr-body-muted');
  });

  it('does not dim the body for a live (Approved) ADR', () => {
    result({ data: DETAIL });
    const { container } = setup();
    expect(container.querySelector('.adr-body')).not.toHaveClass('adr-body-muted');
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
