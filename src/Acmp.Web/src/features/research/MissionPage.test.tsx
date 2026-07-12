import { describe, it, expect, beforeEach, vi, type Mock } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import axe from 'axe-core';
import { MissionPage } from './MissionPage';
import { AcmpAuthContext } from '../../auth/AcmpAuthContext';
import { makeAuth } from '../../test/render';
import { ApiError } from '../../api/apiClient';
import type { CommitteeRole } from '../../auth/roles';
import type { MissionDetail } from '../../api/research';

const fns = vi.hoisted(() => ({
  activate: vi.fn().mockResolvedValue(undefined),
  complete: vi.fn().mockResolvedValue(undefined),
  cancel: vi.fn().mockResolvedValue(undefined),
  addFinding: vi.fn().mockResolvedValue(undefined),
  verify: vi.fn().mockResolvedValue(undefined),
  addRec: vi.fn().mockResolvedValue(undefined),
  setStatus: vi.fn().mockResolvedValue(undefined),
}));

vi.mock('../../api/research', async (orig) => ({
  ...(await orig<typeof import('../../api/research')>()),
  useMission: vi.fn(),
  useActivateMission: () => ({ mutateAsync: fns.activate, isPending: false }),
  useCompleteMission: () => ({ mutateAsync: fns.complete, isPending: false }),
  useCancelMission: () => ({ mutateAsync: fns.cancel, isPending: false }),
  useAddFinding: () => ({ mutateAsync: fns.addFinding, isPending: false }),
  useVerifyFinding: () => ({ mutateAsync: fns.verify, isPending: false }),
  useAddRecommendation: () => ({ mutateAsync: fns.addRec, isPending: false }),
  useSetRecommendationStatus: () => ({ mutateAsync: fns.setStatus, isPending: false }),
}));
import { useMission } from '../../api/research';

const mockMission = useMission as unknown as Mock;

const ACTIVE: MissionDetail = {
  id: 'm1', key: 'RSCH-2026-005',
  title: { en: 'Evaluate a unified IdP', ar: 'تقييم موفّر هوية موحّد' },
  question: { en: 'Does one IdP cut per-stream maintenance?', ar: 'سؤال' },
  status: 'Active', ownerUserId: 'kc-noura', ownerName: 'Noura P',
  keystonePackageRef: 'KS-2026-014', sourceTopicId: 'g-topic',
  completedAt: null, cancellationReason: null,
  findings: [
    { id: 'f1', key: 'F-1', summary: { en: 'Two streams issue incompatible tokens', ar: 's' }, detail: { en: 'Audit export, 18 May', ar: 'd' }, confidence: 'High', isVerified: false },
    { id: 'f2', key: 'F-2', summary: { en: 'Realm-per-stream meets isolation', ar: 's' }, detail: null, confidence: 'Medium', isVerified: true },
  ],
  recommendations: [
    { id: 'r1', key: 'R-1', statement: { en: 'Adopt Keycloak, realm-per-stream', ar: 's' }, rationale: { en: 'proven', ar: 'r' }, priority: 'High', status: 'Proposed', linkedTopicId: null },
    { id: 'r2', key: 'R-2', statement: { en: 'Require a rollback before cutover', ar: 's' }, rationale: null, priority: 'Medium', status: 'Accepted', linkedTopicId: null },
  ],
  createdAt: '2026-06-02T09:00:00Z',
};
const PROPOSED: MissionDetail = { ...ACTIVE, status: 'Proposed', findings: [], recommendations: [], keystonePackageRef: null, sourceTopicId: null };
const COMPLETED: MissionDetail = { ...ACTIVE, status: 'Completed', completedAt: '2026-06-20T09:00:00Z' };
const CANCELLED: MissionDetail = { ...ACTIVE, status: 'Cancelled', cancellationReason: { en: 'Deprioritised after the pivot', ar: 'سبب' } };

function result(over: Partial<ReturnType<typeof useMission>>) {
  mockMission.mockReturnValue({ data: undefined, isLoading: false, isError: false, error: null, refetch: vi.fn(), ...over } as ReturnType<typeof useMission>);
}
function setup(roles: CommitteeRole[] = ['chairman']) {
  return render(
    <AcmpAuthContext.Provider value={makeAuth(roles)}>
      <MemoryRouter initialEntries={['/research/RSCH-2026-005']}>
        <Routes>
          <Route path="/research/:key" element={<MissionPage />} />
        </Routes>
      </MemoryRouter>
    </AcmpAuthContext.Provider>,
  );
}

describe('MissionPage (P15b)', () => {
  beforeEach(() => {
    mockMission.mockReset();
    Object.values(fns).forEach((f) => f.mockReset().mockResolvedValue(undefined));
  });

  it('shows the loading state while fetching', () => {
    result({ isLoading: true });
    setup();
    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();
  });

  it('shows a not-found empty state on 404', () => {
    result({ isError: true, error: new ApiError(404, undefined) });
    setup();
    expect(screen.getByText('Mission not found')).toBeInTheDocument();
  });

  it('shows a retryable error state on a non-404 failure', async () => {
    const refetch = vi.fn();
    result({ isError: true, error: new ApiError(500, undefined), refetch });
    setup();
    await userEvent.click(screen.getByRole('button', { name: /retry/i }));
    expect(refetch).toHaveBeenCalled();
  });

  it('renders the header, alt line, facts, question, findings and recommendations from the DTO', () => {
    result({ data: ACTIVE });
    setup();
    expect(screen.getByRole('heading', { name: 'Evaluate a unified IdP' })).toBeInTheDocument();
    // Alt line (opposite direction) renders the Arabic mirror since en !== ar.
    expect(screen.getByText('تقييم موفّر هوية موحّد')).toBeInTheDocument();
    expect(screen.getByText('In discovery')).toBeInTheDocument();
    expect(screen.getByText('Linked topic')).toBeInTheDocument();
    expect(screen.getByText('Noura P')).toBeInTheDocument();
    expect(screen.getByText('KS-2026-014')).toBeInTheDocument();
    expect(screen.getByText('Does one IdP cut per-stream maintenance?')).toBeInTheDocument();
    expect(screen.getByText('Two streams issue incompatible tokens')).toBeInTheDocument();
    expect(screen.getByText('Unverified')).toBeInTheDocument();
    expect(screen.getByText('Verified')).toBeInTheDocument();
    expect(screen.getByText('Adopt Keycloak, realm-per-stream')).toBeInTheDocument();
  });

  it('omits the alt line when the title is identical in both locales', () => {
    result({ data: { ...ACTIVE, title: { en: 'Same title', ar: 'Same title' } } });
    setup();
    expect(screen.getAllByText('Same title')).toHaveLength(1);
  });

  it('Active + Chair/Sec: shows Conclude/Cancel/Add/Verify/Accept/Reject and no Activate', () => {
    result({ data: ACTIVE });
    setup(['secretary']);
    expect(screen.queryByRole('button', { name: 'Start discovery' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Conclude' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cancel mission' })).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Add' })).toHaveLength(2);
    // Only the unverified finding offers Verify; only the Proposed rec offers Accept/Reject.
    expect(screen.getAllByRole('button', { name: 'Verify' })).toHaveLength(1);
    expect(screen.getByRole('button', { name: 'Accept' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Reject' })).toBeInTheDocument();
  });

  it('Proposed + Chair/Sec: offers Activate/Cancel only, with empty section messages', () => {
    result({ data: PROPOSED });
    setup();
    expect(screen.getByRole('button', { name: 'Start discovery' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cancel mission' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Conclude' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Add' })).not.toBeInTheDocument();
    expect(screen.getByText('No findings captured yet.')).toBeInTheDocument();
    expect(screen.getByText('No recommendations captured yet.')).toBeInTheDocument();
  });

  it('Completed: is read-only — no lifecycle or item affordances', () => {
    result({ data: COMPLETED });
    setup();
    ['Start discovery', 'Conclude', 'Cancel mission', 'Add', 'Verify', 'Accept', 'Reject'].forEach((name) => {
      expect(screen.queryByRole('button', { name })).not.toBeInTheDocument();
    });
    // Still readable.
    expect(screen.getByRole('heading', { name: 'Evaluate a unified IdP' })).toBeInTheDocument();
  });

  it('Cancelled: shows the cancellation reason banner', () => {
    result({ data: CANCELLED });
    setup();
    expect(screen.getByText(/Deprioritised after the pivot/)).toBeInTheDocument();
  });

  it('Member on an Active mission: no mutating affordances (API denies non-Chair/Sec)', () => {
    result({ data: ACTIVE });
    setup(['member']);
    ['Conclude', 'Cancel mission', 'Add', 'Verify', 'Accept', 'Reject'].forEach((name) => {
      expect(screen.queryByRole('button', { name })).not.toBeInTheDocument();
    });
    expect(screen.getByRole('heading', { name: 'Evaluate a unified IdP' })).toBeInTheDocument();
  });

  it('activates a Proposed mission', async () => {
    result({ data: PROPOSED });
    setup();
    await userEvent.click(screen.getByRole('button', { name: 'Start discovery' }));
    expect(fns.activate).toHaveBeenCalledWith({ id: 'm1' });
  });

  it('completes an Active mission', async () => {
    result({ data: ACTIVE });
    setup();
    await userEvent.click(screen.getByRole('button', { name: 'Conclude' }));
    expect(fns.complete).toHaveBeenCalledWith({ id: 'm1' });
  });

  it('verifies an unverified finding', async () => {
    result({ data: ACTIVE });
    setup();
    await userEvent.click(screen.getByRole('button', { name: 'Verify' }));
    expect(fns.verify).toHaveBeenCalledWith({ id: 'm1', findingId: 'f1' });
  });

  it('accepts and rejects a Proposed recommendation', async () => {
    const user = userEvent.setup();
    result({ data: ACTIVE });
    setup();
    await user.click(screen.getByRole('button', { name: 'Accept' }));
    expect(fns.setStatus).toHaveBeenCalledWith({ id: 'm1', recommendationId: 'r1', status: 'Accepted' });
    await user.click(screen.getByRole('button', { name: 'Reject' }));
    expect(fns.setStatus).toHaveBeenCalledWith({ id: 'm1', recommendationId: 'r1', status: 'Rejected' });
  });

  it('cancels a mission: validates the reason, then posts it mirrored', async () => {
    const user = userEvent.setup();
    result({ data: ACTIVE });
    setup();
    await user.click(screen.getByRole('button', { name: 'Cancel mission' }));
    await user.click(screen.getByRole('button', { name: 'Confirm cancellation' }));
    expect(screen.getByText('A reason is required.')).toBeInTheDocument();
    expect(fns.cancel).not.toHaveBeenCalled();
    await user.type(screen.getByLabelText(/Reason/), 'Superseded by a broader review');
    await user.click(screen.getByRole('button', { name: 'Confirm cancellation' }));
    expect(fns.cancel).toHaveBeenCalledWith({ id: 'm1', reason: { en: 'Superseded by a broader review', ar: 'Superseded by a broader review' } });
  });

  it('adds a finding: validates the summary, changes confidence, then posts it', async () => {
    const user = userEvent.setup();
    result({ data: ACTIVE });
    setup();
    await user.click(screen.getAllByRole('button', { name: 'Add' })[0]);
    const dialog = screen.getByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Add finding' }));
    expect(screen.getByText('A summary is required.')).toBeInTheDocument();
    await user.type(within(dialog).getByLabelText(/Summary/), 'Token lifetimes already diverge');
    await user.type(within(dialog).getByLabelText(/Detail/), 'Seen in the audit export');
    await user.click(within(dialog).getByRole('button', { name: 'Confidence' }));
    await user.click(screen.getByRole('option', { name: 'High confidence' }));
    await user.click(within(dialog).getByRole('button', { name: 'Add finding' }));
    expect(fns.addFinding).toHaveBeenCalledWith({
      id: 'm1',
      summary: { en: 'Token lifetimes already diverge', ar: 'Token lifetimes already diverge' },
      detail: { en: 'Seen in the audit export', ar: 'Seen in the audit export' },
      confidence: 'High',
    });
  });

  it('surfaces a server error when adding a finding', async () => {
    const user = userEvent.setup();
    fns.addFinding.mockRejectedValueOnce(new ApiError(409, { title: 'Mission is not active' }));
    result({ data: ACTIVE });
    setup();
    await user.click(screen.getAllByRole('button', { name: 'Add' })[0]);
    const dialog = screen.getByRole('dialog');
    await user.type(within(dialog).getByLabelText(/Summary/), 'X');
    await user.click(within(dialog).getByRole('button', { name: 'Add finding' }));
    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Mission is not active');
  });

  it('adds a recommendation: validates the statement, then posts it (rationale optional)', async () => {
    const user = userEvent.setup();
    result({ data: ACTIVE });
    setup();
    await user.click(screen.getAllByRole('button', { name: 'Add' })[1]);
    const dialog = screen.getByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Add recommendation' }));
    expect(screen.getByText('A statement is required.')).toBeInTheDocument();
    await user.type(within(dialog).getByLabelText(/Statement/), 'Adopt Keycloak as the standard IdP');
    await user.click(within(dialog).getByRole('button', { name: 'Add recommendation' }));
    expect(fns.addRec).toHaveBeenCalledWith({
      id: 'm1',
      statement: { en: 'Adopt Keycloak as the standard IdP', ar: 'Adopt Keycloak as the standard IdP' },
      rationale: null,
      priority: 'Medium',
    });
  });

  it('is axe-clean on an Active mission (WCAG 2.2 AA structure/ARIA)', async () => {
    result({ data: ACTIVE });
    setup();
    const results = await axe.run(document.body, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(results.violations.map((v) => v.id)).toEqual([]);
  });
});
