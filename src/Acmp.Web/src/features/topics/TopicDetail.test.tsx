import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import axe from 'axe-core';
import { TopicDetail } from './TopicDetail';
import { AcmpAuthContext } from '../../auth/AcmpAuthContext';
import { makeAuth } from '../../test/render';
import { ApiError } from '../../api/apiClient';
import type { TopicDetail as Topic } from '../../api/topics';

// The traceability panel (which replaced the P5 empty relationships sidebar) has its own test; stub
// it here so this page test stays isolated from the panel's query providers.
vi.mock('../traceability/TraceabilityPanel', () => ({ TraceabilityPanel: () => 'TRACE_PANEL' }));
// ⚠ EVERY HOOK THE COMPONENT IMPORTS MUST BE LISTED HERE. A module mock REPLACES the module, so a
// hook added to TopicDetail.tsx and forgotten here is `undefined` at render and the whole suite dies
// on "is not a function" — which is how a label rename once turned main red (PE-409).
vi.mock('../../api/topics', () => ({
  useTopicDetail: vi.fn(), useAddTopicComment: vi.fn(), useUploadTopicAttachment: vi.fn(),
  usePrepareTopic: vi.fn(), useReactivateTopic: vi.fn(), useCloseTopic: vi.fn(), useReopenTopic: vi.fn(),
  useConvertTopic: vi.fn(),
}));
import {
  useTopicDetail, useAddTopicComment, useUploadTopicAttachment, usePrepareTopic,
  useReactivateTopic, useCloseTopic, useReopenTopic, useConvertTopic,
} from '../../api/topics';

const mockDetail = useTopicDetail as unknown as Mock;
const mockAddComment = useAddTopicComment as unknown as Mock;
const mockUpload = useUploadTopicAttachment as unknown as Mock;
const mockPrepare = usePrepareTopic as unknown as Mock;
const mockReactivate = useReactivateTopic as unknown as Mock;
const mockClose = useCloseTopic as unknown as Mock;
const mockReopen = useReopenTopic as unknown as Mock;
const mockConvert = useConvertTopic as unknown as Mock;
let mutate: Mock;
let uploadMutate: Mock;
let prepareMutate: Mock;
let reactivateMutate: Mock;
let closeMutate: Mock;
let reopenMutate: Mock;
let convertMutate: Mock;

const TOPIC: Topic = {
  restricted: false,
  id: 'g1', key: 'TOP-2026-014', title: 'Adopt Keycloak as the standard IdP', description: 'Consolidate IdP onto Keycloak.',
  justification: 'Reduce auth sprawl and audit cost.', type: 'ArchitectureDecision', status: 'Scheduled', urgency: 'Urgent',
  scope: 'MultiStream', source: 'CommitteeMember', streams: ['identity', 'platform'], systems: ['Auth Service'], tags: [],
  ownerId: 'o1', ownerName: 'Omar H', submittedByName: 'Omar H', priority: 1, ageDays: 9, slaBreached: false,
  createdAt: '2026-02-15T09:00:00Z', revisitOn: null,
  history: [
    { from: '', to: 'Submitted', reason: null, actorName: 'Omar H', occurredAt: '2026-02-15T09:00:00Z' },
    { from: 'Triage', to: 'Accepted', reason: 'Looks good', actorName: 'Khalid A', occurredAt: '2026-02-15T14:00:00Z' },
  ],
  comments: [{ id: 'c1', body: 'We must document a rollback path.', authorName: 'Noura P', postedAt: '2026-02-16T09:00:00Z' }],
  attachments: [{ id: 'a1', fileName: 'eval.pdf', contentType: 'application/pdf', sizeBytes: 1400, uploadedByName: 'Omar H', uploadedAt: '2026-02-15T10:00:00Z' }],
};

function result(over: Partial<ReturnType<typeof useTopicDetail>>) {
  mockDetail.mockReturnValue({ data: undefined, isLoading: false, isError: false, error: null, refetch: vi.fn(), ...over });
}

function setup(path = '/topics/TOP-2026-014') {
  render(
    <AcmpAuthContext.Provider value={makeAuth(['secretary'])}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/topics/:key" element={<TopicDetail />} />
        </Routes>
      </MemoryRouter>
    </AcmpAuthContext.Provider>,
  );
}

describe('TopicDetail (P5b)', () => {
  beforeEach(() => {
    mockDetail.mockReset();
    mutate = vi.fn();
    mockAddComment.mockReturnValue({ mutate, isPending: false });
    mockUpload.mockReset();
    uploadMutate = vi.fn();
    mockUpload.mockReturnValue({ mutate: uploadMutate, isPending: false });
    mockPrepare.mockReset();
    prepareMutate = vi.fn();
    mockPrepare.mockReturnValue({ mutate: prepareMutate, isPending: false });
    mockReactivate.mockReset();
    reactivateMutate = vi.fn();
    mockReactivate.mockReturnValue({ mutate: reactivateMutate, isPending: false });
    mockClose.mockReset();
    closeMutate = vi.fn();
    mockClose.mockReturnValue({ mutate: closeMutate, isPending: false });
    mockReopen.mockReset();
    reopenMutate = vi.fn();
    mockReopen.mockReturnValue({ mutate: reopenMutate, isPending: false });
    mockConvert.mockReset();
    convertMutate = vi.fn();
    mockConvert.mockReturnValue({ mutate: convertMutate, isPending: false });
  });

  // FR-160 / FR-161 / FR-045 — the lifecycle exits. Each button is gated on the ONE status its
  // transition accepts, so the assertions pair "appears on the right status" with "absent on the
  // default one" — a button that always rendered would satisfy a presence-only test while offering
  // the user an action the server refuses.
  it('offers Return to triage only on a Deferred topic, and calls the mutation', async () => {
    result({ data: { ...TOPIC, status: 'Deferred' } });
    setup();
    await userEvent.click(screen.getByRole('button', { name: /Return to triage/i }));
    expect(reactivateMutate).toHaveBeenCalledWith('g1', expect.anything());
  });

  it('offers Close topic only on a Decided topic, and calls the mutation', async () => {
    result({ data: { ...TOPIC, status: 'Decided' } });
    setup();
    await userEvent.click(screen.getByRole('button', { name: /Close topic/i }));
    expect(closeMutate).toHaveBeenCalledWith('g1', expect.anything());
  });

  it('does not offer the lifecycle exits on a Scheduled topic', () => {
    result({ data: TOPIC });   // Scheduled
    setup();
    expect(screen.queryByRole('button', { name: /Return to triage/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Close topic/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Reopen$/i })).not.toBeInTheDocument();
  });

  // AC-112: the justification is mandatory, and the dialog enforces it BEFORE the request rather
  // than letting the server refuse something the UI could have prevented.
  it('requires a justification before a reopen can be confirmed', async () => {
    result({ data: { ...TOPIC, status: 'Rejected' } });
    setup();
    await userEvent.click(screen.getByRole('button', { name: /^Reopen$/i }));

    const confirm = screen.getByRole('button', { name: /Reopen topic/i });
    expect(confirm).toBeDisabled();

    await userEvent.type(screen.getByLabelText(/Reason for reopening/i), 'new regulatory guidance');
    expect(confirm).toBeEnabled();

    await userEvent.click(confirm);
    expect(reopenMutate).toHaveBeenCalledWith(
      { topicId: 'g1', reason: 'new regulatory guidance' }, expect.anything());
  });

  // AC-113 / FR-030. Both inputs are mandatory and the confirm stays disabled until BOTH are given —
  // asserted separately, because a test that types only the reason would pass against a dialog that
  // had forgotten to gate on the type, and a mis-clicked convert retires a Decided topic irreversibly.
  it('offers Convert type only on a Decided topic', async () => {
    result({ data: { ...TOPIC, status: 'Decided' } });
    setup();
    expect(screen.getByRole('button', { name: /Convert type/i })).toBeInTheDocument();
  });

  it('does not offer Convert type on a non-Decided topic', () => {
    result({ data: TOPIC }); // Scheduled
    setup();
    expect(screen.queryByRole('button', { name: /Convert type/i })).not.toBeInTheDocument();
  });

  it('requires BOTH a target type and a reason before a conversion can be confirmed', async () => {
    result({ data: { ...TOPIC, status: 'Decided' } });
    setup();
    await userEvent.click(screen.getByRole('button', { name: /Convert type/i }));

    const confirm = screen.getByRole('button', { name: /Convert topic/i });
    expect(confirm).toBeDisabled();

    // Reason alone is not enough.
    await userEvent.type(screen.getByLabelText(/Reason for converting/i), 'research concluded');
    expect(confirm).toBeDisabled();

    await userEvent.selectOptions(screen.getByLabelText(/Convert to/i), 'ResearchDiscovery');
    expect(confirm).toBeEnabled();

    await userEvent.click(confirm);
    expect(convertMutate).toHaveBeenCalledWith(
      { topicId: 'g1', targetType: 'ResearchDiscovery', reason: 'research concluded' }, expect.anything());
  });

  it('omits the topic\'s current type from the conversion choices', async () => {
    result({ data: { ...TOPIC, status: 'Decided' } }); // type = ArchitectureDecision
    setup();
    await userEvent.click(screen.getByRole('button', { name: /Convert type/i }));

    const options = within(screen.getByLabelText(/Convert to/i)).getAllByRole('option');
    // placeholder + the three OTHER types; offering the current one is an option that can only fail.
    expect(options).toHaveLength(4);
    expect(options.map((o: HTMLElement) => (o as HTMLOptionElement).value)).not.toContain('ArchitectureDecision');
  });

  it('renders the header and overview from the detail DTO', () => {
    result({ data: TOPIC });
    setup();
    expect(screen.getByRole('heading', { name: 'Adopt Keycloak as the standard IdP' })).toBeInTheDocument();
    expect(screen.getAllByText('TOP-2026-014').length).toBeGreaterThanOrEqual(1); // breadcrumb + key chip
    expect(screen.getByText('Scheduled')).toBeInTheDocument();
    expect(screen.getByText('Consolidate IdP onto Keycloak.')).toBeInTheDocument();
    expect(screen.getByText('Reduce auth sprawl and audit cost.')).toBeInTheDocument();
    expect(screen.getByText('Auth Service')).toBeInTheDocument(); // affected system tag
  });

  it('shows the urgent chip for a non-Normal urgency', () => {
    result({ data: TOPIC });
    setup();
    // Urgent appears in the header chip + the urgency text.
    expect(screen.getAllByText('Urgent').length).toBeGreaterThanOrEqual(1);
  });

  it('mounts the traceability panel in the sidebar (P10e replaced the P5 empty state)', () => {
    result({ data: TOPIC });
    setup();
    expect(screen.getByText('TRACE_PANEL')).toBeInTheDocument();
  });

  it('switches to Discussion and posts a comment by topic id', async () => {
    result({ data: TOPIC });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('tab', { name: /Discussion/ }));
    expect(screen.getByText('We must document a rollback path.')).toBeInTheDocument();
    await user.type(screen.getByLabelText('Add a comment'), 'Agree — link the rollback ADR.');
    await user.click(screen.getByRole('button', { name: 'Post comment' }));
    expect(mutate).toHaveBeenCalledWith(
      { topicId: 'g1', body: 'Agree — link the rollback ADR.' },
      expect.anything(),
    );
  });

  it('moves attachments to their own tab and uploads a dropped file to the topic id', async () => {
    result({ data: TOPIC });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('tab', { name: /Attachments/ }));
    expect(screen.getByText('eval.pdf')).toBeInTheDocument(); // existing attachment listed in the tab
    const file = new File(['x'], 'design.pdf', { type: 'application/pdf' });
    await user.upload(screen.getByLabelText(/Drop files/i), file);
    expect(uploadMutate).toHaveBeenCalledWith({ topicId: 'g1', file });
  });

  it('renders the Votes tab as an honest empty state (Voting → P9)', async () => {
    result({ data: TOPIC });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('tab', { name: /Votes/ }));
    expect(screen.getByText('No votes yet')).toBeInTheDocument();
  });

  it('switches to History and renders the status timeline', async () => {
    result({ data: TOPIC });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('tab', { name: /History/ }));
    expect(screen.getByText(/Triage → Accepted/)).toBeInTheDocument();
    expect(screen.getAllByText(/Looks good/).length).toBeGreaterThanOrEqual(1);
  });

  it('offers Mark prepared for an Accepted topic and calls prepare with the topic id (D-15)', async () => {
    result({ data: { ...TOPIC, status: 'Accepted' } });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Mark prepared' }));
    expect(prepareMutate).toHaveBeenCalledWith('g1', expect.anything());
  });

  it('hides Mark prepared when the topic is not Accepted', () => {
    result({ data: TOPIC }); // Scheduled
    setup();
    expect(screen.queryByRole('button', { name: 'Mark prepared' })).not.toBeInTheDocument();
  });

  it('surfaces a 403 from prepare inline instead of failing silently', async () => {
    prepareMutate.mockImplementation((_id: string, opts: { onError: (e: unknown) => void }) => opts.onError(new ApiError(403)));
    result({ data: { ...TOPIC, status: 'Accepted' } });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Mark prepared' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(/permission/i);
  });

  it('renders a not-found state for a 404', () => {
    result({ isError: true, error: new ApiError(404) });
    setup('/topics/TOP-9999-999');
    expect(screen.getByText('Topic not found')).toBeInTheDocument();
  });

  it('shows the loading state while fetching', () => {
    result({ isLoading: true });
    setup();
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('is axe-clean (WCAG 2.2 AA structure/ARIA)', async () => {
    result({ data: TOPIC });
    setup();
    const results = await axe.run(document.body, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(results.violations.map((v) => v.id)).toEqual([]);
  });
});
