import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route, useLocation } from 'react-router-dom';
import axe from 'axe-core';
import { DecisionPage } from './DecisionPage';

// The traceability panel has its own test (TraceabilityPanel.test.tsx); stub it here so this page
// test stays isolated from the panel's auth/query providers.
vi.mock('../traceability/TraceabilityPanel', () => ({ TraceabilityPanel: () => 'TRACE_PANEL' }));
import { AcmpAuthContext } from '../../auth/AcmpAuthContext';
import { makeAuth } from '../../test/render';
import { ApiError } from '../../api/apiClient';
import type { DecisionDetail } from '../../api/decisions';
import type { CommitteeRole } from '../../auth/roles';

vi.mock('../../api/decisions', async (orig) => ({
  ...(await orig<typeof import('../../api/decisions')>()),
  useDecision: vi.fn(),
  useSupersedeDecision: vi.fn(),
}));
import { useDecision, useSupersedeDecision } from '../../api/decisions';

// The (always-mounted) CreateActionDialog pulls the member directory + create mutation; stub them so this
// suite needs no QueryClient. Its own behaviour is covered in CreateActionDialog.test.tsx.
vi.mock('../../api/members', () => ({ useMembers: () => ({ data: [] }) }));
vi.mock('../../api/actions', () => ({ useCreateAction: () => ({ mutateAsync: vi.fn(), isPending: false }) }));
// The (always-mounted) ConvertToAdrDialog uses the promote mutation; stub it so this suite needs no
// QueryClient. Its own behaviour is covered in ConvertToAdrDialog.test.tsx.
vi.mock('../../api/adrs', () => ({ usePromoteDecisionToAdr: () => ({ mutateAsync: vi.fn(), isPending: false }) }));

const mockDecision = useDecision as unknown as Mock;
const mockSupersede = useSupersedeDecision as unknown as Mock;
let mutateAsync: Mock;

const ISSUED: DecisionDetail = {
  id: 'p1', key: 'DECN-2026-008', topicId: 't-guid', meetingId: 'm-guid',
  outcome: 'ConditionallyApproved', status: 'Issued',
  title: { en: 'Adopt Keycloak as the standard IdP', ar: 'اعتماد كيكلوك' },
  statement: { en: 'The committee adopts Keycloak as the standard identity provider.', ar: 'تعتمد اللجنة كيكلوك.' },
  rationale: { en: 'Consolidate identity onto one proven platform.', ar: 'توحيد الهوية.' },
  alternatives: { en: 'Build an in-house IdP — higher burden.', ar: 'بناء داخلي.' },
  voteId: null, chairApprovedByUserId: 'kc-chair', chairApprovedByName: 'Sara Chair',
  chairOverride: false, overrideJustification: null, issuedAt: '2026-02-18T10:35:00Z',
  supersededByDecisionId: null, supersessionReason: null,
  conditions: [{ id: 'c1', text: { en: 'A rollback plan is in place.', ar: 'خطة تراجع.' }, status: 'Open', dueDate: null, linkedActionId: null }],
};

const SUPERSEDED: DecisionDetail = {
  ...ISSUED, status: 'Superseded', supersededByDecisionId: 's-guid',
  supersessionReason: { en: 'Replaced after the federated-IdP pivot.', ar: 'استُبدل.' },
};

function result(over: Partial<ReturnType<typeof useDecision>>) {
  mockDecision.mockReturnValue({ data: undefined, isLoading: false, isError: false, error: null, refetch: vi.fn(), ...over });
}

function LocationDisplay() {
  return <div data-testid="loc">{useLocation().pathname}</div>;
}

function setup(roles: CommitteeRole[] = ['chairman'], path = '/decisions/DECN-2026-008') {
  return render(
    <AcmpAuthContext.Provider value={makeAuth(roles)}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/decisions/:key" element={<DecisionPage />} />
        </Routes>
        <LocationDisplay />
      </MemoryRouter>
    </AcmpAuthContext.Provider>,
  );
}

describe('DecisionPage (P7b)', () => {
  beforeEach(() => {
    mockDecision.mockReset();
    mutateAsync = vi.fn().mockResolvedValue({ id: 's1', key: 'DECN-2026-015' });
    mockSupersede.mockReturnValue({ mutateAsync, isPending: false });
  });

  it('shows the loading state while fetching', () => {
    result({ isLoading: true });
    setup();
    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();
  });

  it('shows a not-found empty state on 404', () => {
    result({ isError: true, error: new ApiError(404, undefined) });
    setup();
    expect(screen.getByText('Decision not found')).toBeInTheDocument();
  });

  it('shows a retryable error state on a non-404 failure', () => {
    result({ isError: true, error: new ApiError(500, undefined) });
    setup();
    expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
  });

  it('renders header, rationale, conditions and record detail from the DTO', () => {
    result({ data: ISSUED });
    setup();
    expect(screen.getByRole('heading', { name: 'Adopt Keycloak as the standard IdP' })).toBeInTheDocument();
    expect(screen.getAllByText('DECN-2026-008').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Conditionally Approved').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('Consolidate identity onto one proven platform.')).toBeInTheDocument();
    expect(screen.getByText('A rollback plan is in place.')).toBeInTheDocument();
    expect(screen.getByText('Sara Chair')).toBeInTheDocument();
  });

  it('renders the superseded state (badge + banner) and hides actions', () => {
    result({ data: SUPERSEDED });
    setup();
    expect(screen.getByText('Superseded')).toBeInTheDocument();
    expect(screen.getByText(/Replaced after the federated-IdP pivot/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Supersede' })).not.toBeInTheDocument();
  });

  it('offers the Supersede action to a chairman only', () => {
    result({ data: ISSUED });
    setup(['chairman']);
    expect(screen.getByRole('button', { name: 'Supersede' })).toBeInTheDocument();
  });

  it('hides the Supersede action from a non-chair', () => {
    result({ data: ISSUED });
    setup(['secretary']);
    expect(screen.queryByRole('button', { name: 'Supersede' })).not.toBeInTheDocument();
  });

  it('offers Convert to ADR to a chairman and opens the confirm dialog (FR-068)', async () => {
    const user = userEvent.setup();
    result({ data: ISSUED });
    setup(['chairman']);
    await user.click(screen.getByRole('button', { name: 'Convert to ADR' }));
    // The confirm dialog names the source decision in its body.
    expect(screen.getByText(/pre-filled from DECN-2026-008/)).toBeInTheDocument();
  });

  it('hides Convert to ADR from a non-chair', () => {
    result({ data: ISSUED });
    setup(['secretary']);
    expect(screen.queryByRole('button', { name: 'Convert to ADR' })).not.toBeInTheDocument();
  });

  it('validates the supersede dialog before submitting', async () => {
    const user = userEvent.setup();
    result({ data: ISSUED });
    setup(['chairman']);
    await user.click(screen.getByRole('button', { name: 'Supersede' }));
    await user.click(screen.getByRole('button', { name: 'Supersede decision' }));
    expect(screen.getByText('A title is required.')).toBeInTheDocument();
    expect(mutateAsync).not.toHaveBeenCalled();
  });

  it('supersedes with the full successor body mirrored to both locales, then navigates', async () => {
    const user = userEvent.setup();
    result({ data: ISSUED });
    setup(['chairman']);
    await user.click(screen.getByRole('button', { name: 'Supersede' }));

    await user.type(screen.getByLabelText(/Title/), 'Adopt federated IdP');
    await user.type(screen.getByLabelText(/Decision statement/), 'The committee adopts a federated IdP');
    await user.type(screen.getByLabelText(/Rationale/), 'Pivoted to federation');
    await user.type(screen.getByLabelText(/Reason for superseding/), 'Federated pivot');
    await user.click(screen.getByRole('button', { name: 'Supersede decision' }));

    expect(mutateAsync).toHaveBeenCalledTimes(1);
    const arg = mutateAsync.mock.calls[0][0];
    expect(arg.priorDecisionId).toBe('p1');
    // Content is mirrored into both bilingual columns (en === ar).
    expect(arg.title).toEqual({ en: 'Adopt federated IdP', ar: 'Adopt federated IdP' });
    expect(arg.reason).toEqual({ en: 'Federated pivot', ar: 'Federated pivot' });
    // `loc` is always mounted, so findByTestId would resolve on its first poll and toHaveTextContent
    // would evaluate once — before onConfirm's post-await navigate() flushed (D-19). Retry the
    // assertion, not the element.
    await waitFor(() => expect(screen.getByTestId('loc')).toHaveTextContent('/decisions/DECN-2026-015'));
  });

  it('includes optional alternatives in the successor body when provided', async () => {
    const user = userEvent.setup();
    result({ data: ISSUED });
    setup(['chairman']);
    await user.click(screen.getByRole('button', { name: 'Supersede' }));
    await user.type(screen.getByLabelText(/^Title/), 'New title');
    await user.type(screen.getByLabelText(/Decision statement/), 'The committee decides anew');
    await user.type(screen.getByLabelText(/^Rationale/), 'New rationale');
    await user.type(screen.getByLabelText(/Alternatives considered/), 'We weighed a SaaS IdP');
    await user.type(screen.getByLabelText(/Reason for superseding/), 'pivot');
    await user.click(screen.getByRole('button', { name: 'Supersede decision' }));
    expect(mutateAsync.mock.calls[0][0].alternatives).toEqual({ en: 'We weighed a SaaS IdP', ar: 'We weighed a SaaS IdP' });
  });

  it('requires a condition when the successor is Conditionally Approved, then submits it', async () => {
    const user = userEvent.setup();
    result({ data: ISSUED });
    setup(['chairman']);
    await user.click(screen.getByRole('button', { name: 'Supersede' }));

    // switch the outcome to Conditionally Approved → the conditions editor appears
    await user.click(screen.getByRole('button', { name: 'Outcome' }));
    await user.click(screen.getByRole('option', { name: 'Conditionally Approved' }));

    await user.type(screen.getByLabelText(/^Title/), 'Conditional title');
    await user.type(screen.getByLabelText(/Decision statement/), 'The committee conditionally decides');
    await user.type(screen.getByLabelText(/^Rationale/), 'Conditional rationale');
    await user.type(screen.getByLabelText(/Reason for superseding/), 'why');

    // submit with an empty condition → blocked with the conditions error
    await user.click(screen.getByRole('button', { name: 'Supersede decision' }));
    expect(screen.getByText(/at least one condition/i)).toBeInTheDocument();
    expect(mutateAsync).not.toHaveBeenCalled();

    // add a second row, remove it, fill the first, then submit
    await user.click(screen.getByRole('button', { name: 'Add condition' }));
    await user.click(screen.getByRole('button', { name: 'Remove condition 2' }));
    await user.type(screen.getByLabelText('Condition 1'), 'A rollback plan exists');
    await user.click(screen.getByRole('button', { name: 'Supersede decision' }));

    expect(mutateAsync).toHaveBeenCalledTimes(1);
    expect(mutateAsync.mock.calls[0][0].conditions).toEqual([
      { text: { en: 'A rollback plan exists', ar: 'A rollback plan exists' }, dueDate: null },
    ]);
  });

  it('surfaces a submit error and does not navigate', async () => {
    const user = userEvent.setup();
    mutateAsync.mockRejectedValueOnce(new ApiError(403, { title: 'Forbidden' }));
    result({ data: ISSUED });
    setup(['chairman']);
    await user.click(screen.getByRole('button', { name: 'Supersede' }));
    await user.type(screen.getByLabelText(/^Title/), 'T');
    await user.type(screen.getByLabelText(/Decision statement/), 'S');
    await user.type(screen.getByLabelText(/^Rationale/), 'R');
    await user.type(screen.getByLabelText(/Reason for superseding/), 'x');
    await user.click(screen.getByRole('button', { name: 'Supersede decision' }));
    expect(await screen.findByText('Forbidden')).toBeInTheDocument();
    expect(screen.getByTestId('loc')).toHaveTextContent('/decisions/DECN-2026-008');
  });

  it('is axe-clean (WCAG 2.2 AA structure/ARIA)', async () => {
    result({ data: ISSUED });
    setup();
    const results = await axe.run(document.body, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(results.violations.map((v) => v.id)).toEqual([]);
  });
});
