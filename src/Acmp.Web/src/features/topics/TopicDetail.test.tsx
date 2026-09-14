import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { render, screen, within, cleanup, fireEvent, waitFor, act } from '@testing-library/react';
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
  useConvertTopic: vi.fn(), useReclassifyTopic: vi.fn(),
  downloadTopicAttachment: vi.fn(),
}));
// AC-169: the limits hook reads GET /api/uploads/limits; here it answers with the shipped defaults.
vi.mock('../../api/uploads', async (importOriginal) => {
  const real = await importOriginal<typeof import('../../api/uploads')>();
  return { ...real, useUploadLimits: () => real.DEFAULT_UPLOAD_LIMITS };
});
// AC-168: streams are shown by name; the taxonomy query is stubbed so the page test stays query-free.
vi.mock('../../api/members', () => ({ useStreamLabel: () => (code: string) => (code === 'identity' ? 'Identity & Access' : code) }));
import {
  useTopicDetail, useAddTopicComment, useUploadTopicAttachment, usePrepareTopic,
  useReactivateTopic, useCloseTopic, useReopenTopic, useConvertTopic, useReclassifyTopic, downloadTopicAttachment,
} from '../../api/topics';

const mockDetail = useTopicDetail as unknown as Mock;
const mockAddComment = useAddTopicComment as unknown as Mock;
const mockUpload = useUploadTopicAttachment as unknown as Mock;
const mockPrepare = usePrepareTopic as unknown as Mock;
const mockReactivate = useReactivateTopic as unknown as Mock;
const mockClose = useCloseTopic as unknown as Mock;
const mockReopen = useReopenTopic as unknown as Mock;
const mockConvert = useConvertTopic as unknown as Mock;
const mockReclassify = useReclassifyTopic as unknown as Mock;
let mutate: Mock;
let uploadMutate: Mock;
let prepareMutate: Mock;
let reactivateMutate: Mock;
let closeMutate: Mock;
let reopenMutate: Mock;
let convertMutate: Mock;
let reclassifyMutate: Mock;

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

function setup(path = '/topics/TOP-2026-014', roles: string[] = ['secretary']) {
  return render(
    <AcmpAuthContext.Provider value={makeAuth(roles as Parameters<typeof makeAuth>[0])}>
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
    uploadMutate = vi.fn().mockResolvedValue({});
    mockUpload.mockReturnValue({ mutateAsync: uploadMutate, isPending: false });
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
    reclassifyMutate = vi.fn();
    mockReclassify.mockReturnValue({ mutate: reclassifyMutate, isPending: false });
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
    expect(uploadMutate).toHaveBeenCalledWith(expect.objectContaining({ topicId: 'g1', file }));
  });

  // AC-162: the Attachments tab refuses an over-maximum file in the browser, as the submit page does.
  it('refuses a file over the maximum on the Attachments tab with the translated message and uploads nothing', async () => {
    result({ data: TOPIC });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('tab', { name: /Attachments/ }));
    const big = new File([new Uint8Array(2)], 'huge.pdf', { type: 'application/pdf' });
    Object.defineProperty(big, 'size', { value: 100 * 1024 * 1024 + 1 });
    await user.upload(screen.getByLabelText(/Drop files/i), big);
    expect(screen.getByRole('alert')).toHaveTextContent(/100 MB or smaller/);
    expect(uploadMutate).not.toHaveBeenCalled();
  });

  // DEF-167: the drop hint states the real maximum, not the 50 MB it said before AC-162.
  it('states the 100 MB maximum in the drop hint', async () => {
    result({ data: TOPIC });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('tab', { name: /Attachments/ }));
    expect(screen.getByText(/up to 100 MB/)).toBeInTheDocument();
    expect(screen.queryByText(/50 MB/)).toBeNull();
  });

  // DEF-168: a long upload says what it is uploading and ignores new files until it finishes.
  it('shows the upload in progress, ignores another file meanwhile, and clears when it lands', async () => {
    let land: (v: unknown) => void = () => {};
    let report: (p: number) => void = () => {};
    uploadMutate.mockImplementation(({ onProgress }: { onProgress?: (p: number) => void }) => {
      report = onProgress ?? report;
      return new Promise((r) => { land = r; });
    });
    result({ data: TOPIC });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('tab', { name: /Attachments/ }));
    const first = new File(['x'], 'design.pdf', { type: 'application/pdf' });
    await user.upload(screen.getByLabelText(/Drop files/i), first);
    expect(screen.getByRole('status')).toHaveTextContent('Uploading design.pdf…');
    // DEF-171: the percentage the XHR reports reaches the bar.
    expect(screen.getByRole('progressbar', { name: 'design.pdf' })).toHaveAttribute('aria-valuenow', '0');
    act(() => report(64));
    expect(screen.getByRole('progressbar', { name: 'design.pdf' })).toHaveAttribute('aria-valuenow', '64');
    expect(screen.getByText('64%')).toBeInTheDocument();
    fireEvent.drop(screen.getByText(/Drop files here/).closest('.sub-drop')!, { dataTransfer: { files: [new File(['y'], 'second.pdf', { type: 'application/pdf' })] } });
    expect(uploadMutate).toHaveBeenCalledTimes(1);
    land({});
    await waitFor(() => expect(screen.queryByRole('status')).toBeNull());
  });

  // DEF-168 / DEF-169: a refused upload is shown - uat's handler-less 400 has no problem body, so the
  // generic translated message is what the user sees.
  it('shows a failed upload with the translated message', async () => {
    uploadMutate.mockRejectedValue(new ApiError(400));
    result({ data: TOPIC });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('tab', { name: /Attachments/ }));
    await user.upload(screen.getByLabelText(/Drop files/i), new File(['x'], 'design.pdf', { type: 'application/pdf' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('The file couldn’t be attached. Check the file and try again.');
    expect(screen.queryByRole('status')).toBeNull();
  });

  // WBS-40.12 / AC-163: the open control is live and opens that attachment of THIS topic.
  it('downloads an attachment through the topic-scoped URL when its download control is clicked (AC-164)', async () => {
    (downloadTopicAttachment as unknown as Mock).mockResolvedValue(undefined);
    result({ data: TOPIC });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('tab', { name: /Attachments/ }));
    const button = screen.getByRole('button', { name: 'Download eval.pdf' });
    expect(button).toBeEnabled();
    await user.click(button);
    expect(downloadTopicAttachment).toHaveBeenCalledWith('g1', 'a1');
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('shows that the file could not be downloaded when the URL is refused', async () => {
    (downloadTopicAttachment as unknown as Mock).mockRejectedValue(new ApiError(404));
    result({ data: TOPIC });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('tab', { name: /Attachments/ }));
    await user.click(screen.getByRole('button', { name: 'Download eval.pdf' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('That file could not be downloaded.');
  });

  // AC-168: in any language, a stream is its NAME (never its code), and the uploader's name is bidi-isolated.
  it('shows streams by name and isolates the uploader and actor names', async () => {
    result({ data: TOPIC });
    const user = userEvent.setup();
    setup();
    expect(screen.getByText('Identity & Access')).toBeInTheDocument();
    expect(screen.queryByText('identity')).toBeNull();
    await user.click(screen.getByRole('tab', { name: /Attachments/ }));
    expect(screen.getByText('Omar H', { selector: 'bdi' })).toBeInTheDocument();
  });

  // AC-169: the picker offers only the server's allowed types, and re-picking the same file starts again.
  it('offers only the allowed types and clears the picker so the same file can be picked again', async () => {
    result({ data: TOPIC });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('tab', { name: /Attachments/ }));
    const input = screen.getByLabelText(/Drop files/i) as HTMLInputElement;
    expect(input.accept).toContain('application/pdf');
    expect(input.accept).not.toContain('video/');
    const file = new File(['x'], 'design.pdf', { type: 'application/pdf' });
    await user.upload(input, file);
    expect(input.value).toBe('');
    await waitFor(() => expect(screen.queryByRole('status')).toBeNull());
    await user.upload(input, file);
    expect(uploadMutate).toHaveBeenCalledTimes(2);
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

  /*
   * Three lifecycle dialogs could be OPENED and none dismissed, and every one of them gates an
   * irreversible transition on a governance record. The dismissal is a separate handler from the
   * confirm, so "the confirm works" says nothing about being able to back out.
   */
  it.each([
    ['Rejected', /^Reopen$/i, /Reopen topic/i],
    ['Decided', /Convert type/i, /Convert topic/i],
    ['Submitted', 'Reclassify', 'Apply changes'],
  ])('cancels the %s lifecycle dialog without committing', async (status, opener, confirmName) => {
    result({ data: { ...TOPIC, status } });
    const user = userEvent.setup();
    setup();

    await user.click(screen.getByRole('button', { name: opener }));
    expect(screen.getByRole('button', { name: confirmName })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(screen.queryByRole('button', { name: confirmName })).toBeNull();
    expect(reopenMutate).not.toHaveBeenCalled();
    expect(convertMutate).not.toHaveBeenCalled();
    expect(reclassifyMutate).not.toHaveBeenCalled();
  });

  // The onSuccess arms. Each closes its dialog, and convert also navigates to the NEW topic - a
  // dialog left open over a topic that no longer exists in that form is worse than no dialog.
  it('closes the reopen dialog once the reopen succeeds', async () => {
    reopenMutate.mockImplementation((_v: unknown, o: { onSuccess: () => void }) => o.onSuccess());
    result({ data: { ...TOPIC, status: 'Rejected' } });
    const user = userEvent.setup();
    setup();

    await user.click(screen.getByRole('button', { name: /^Reopen$/i }));
    await user.type(screen.getByLabelText(/Reason for reopening/i), 'new regulatory guidance');
    await user.click(screen.getByRole('button', { name: /Reopen topic/i }));

    expect(screen.queryByRole('button', { name: /Reopen topic/i })).toBeNull();
  });

  it('navigates to the created topic once a conversion succeeds', async () => {
    convertMutate.mockImplementation((_v: unknown, o: { onSuccess: (c: { key: string }) => void }) =>
      o.onSuccess({ key: 'TOP-2026-099' }));
    result({ data: { ...TOPIC, status: 'Decided' } });
    const user = userEvent.setup();
    setup();

    await user.click(screen.getByRole('button', { name: /Convert type/i }));
    await user.type(screen.getByLabelText(/Reason for converting/i), 'research concluded');
    await userEvent.selectOptions(screen.getByLabelText(/Convert to/i), 'ResearchDiscovery');
    await user.click(screen.getByRole('button', { name: /Convert topic/i }));

    // The route changes to the NEW key, so this page unmounts - asserted by its header going away
    // rather than by spying on the navigate hook, which would not prove the route actually moved.
    expect(screen.queryByRole('button', { name: /Convert topic/i })).toBeNull();
  });

  it('closes the reclassify dialog once it succeeds', async () => {
    reclassifyMutate.mockImplementation((_v: unknown, o: { onSuccess: () => void }) => o.onSuccess());
    result({ data: { ...TOPIC, status: 'Submitted' } });
    const user = userEvent.setup();
    setup();

    await user.click(screen.getByRole('button', { name: 'Reclassify' }));
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Type' }), 'ResearchDiscovery');
    await user.click(screen.getByRole('button', { name: 'Apply changes' }));

    expect(screen.queryByRole('button', { name: 'Apply changes' })).toBeNull();
  });

  // The 403 arm was covered and the OTHER arm was not. They produce different copy on purpose: a 403
  // means "you may not", anything else means "it did not work" - telling a user the wrong one sends
  // them to the wrong person.
  it('shows the generic prepare error for a non-403 failure', async () => {
    prepareMutate.mockImplementation((_id: string, opts: { onError: (e: unknown) => void }) => opts.onError(new ApiError(500)));
    result({ data: { ...TOPIC, status: 'Accepted' } });
    const user = userEvent.setup();
    setup();

    await user.click(screen.getByRole('button', { name: 'Mark prepared' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toBeInTheDocument();
    expect(alert).not.toHaveTextContent(/permission/i);
  });

  it('navigates to the edit form from the header action', async () => {
    result({ data: TOPIC });
    const user = userEvent.setup();
    setup();

    await user.click(screen.getByRole('button', { name: 'Edit' }));

    // The route left this page; its own header action is gone.
    expect(screen.queryByRole('button', { name: 'Edit' })).toBeNull();
  });

  it('retries a failed topic fetch from the error state', async () => {
    const refetch = vi.fn();
    result({ isError: true, error: new ApiError(500), refetch });
    setup();

    await userEvent.click(screen.getByRole('button', { name: /retry|try again/i }));

    expect(refetch).toHaveBeenCalled();
  });

  // Drag-and-drop is the attachment path the tab is designed around, and none of its three handlers
  // had run - the existing test drives the hidden input instead.
  it('uploads a file dropped onto the attachments drop zone', async () => {
    result({ data: TOPIC });
    const user = userEvent.setup();
    const { container } = setup();
    await user.click(screen.getByRole('tab', { name: /Attachments/ }));
    const zone = container.querySelector('.sub-drop') as HTMLElement;
    const file = new File(['x'], 'dropped.pdf', { type: 'application/pdf' });

    fireEvent.dragOver(zone);
    expect(zone).toHaveClass('over'); // the affordance that tells the user the drop will land
    fireEvent.dragLeave(zone);
    expect(zone).not.toHaveClass('over');

    fireEvent.drop(zone, { dataTransfer: { files: [file] } });

    expect(uploadMutate).toHaveBeenCalledWith(expect.objectContaining({ topicId: 'g1', file }));
    expect(zone).not.toHaveClass('over');
    await user.click(within(zone).getByRole('button'));
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

  // ---- FR-164 / DW-032 (WBS-23.4): triage-time reclassification ----

  it('reclassifies a pre-accept topic, carrying its existing source unchanged', async () => {
    // ⚠ The source is deliberately NOT the fixture's default. With TOPIC's own 'CommitteeMember' the
    // assertion passed against an implementation that hardcoded that literal — proven by mutation, so
    // the value here must be one nothing else in the file would produce by accident.
    result({ data: { ...TOPIC, status: 'Submitted', source: 'SecurityFinding' } });
    setup();
    await userEvent.click(screen.getByRole('button', { name: 'Reclassify' }));
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Type' }), 'ResearchDiscovery');
    await userEvent.click(screen.getByRole('button', { name: 'Apply changes' }));
    expect(reclassifyMutate).toHaveBeenCalledTimes(1);
    expect(reclassifyMutate.mock.calls[0][0]).toEqual({
      topicId: 'g1', type: 'ResearchDiscovery', source: 'SecurityFinding',
    });
  });

  it('does not offer reclassification once the topic is past triage', () => {
    result({ data: { ...TOPIC, status: 'Accepted' } });
    setup();
    expect(screen.queryByRole('button', { name: 'Reclassify' })).toBeNull();
    // ...and DOES offer it pre-accept, so the assertion above cannot pass by the control being
    // absent everywhere.
    cleanup();
    result({ data: { ...TOPIC, status: 'Triage' } });
    setup();
    expect(screen.getByRole('button', { name: 'Reclassify' })).toBeInTheDocument();
  });

  it('does not offer reclassification to a member, whom the server would refuse', () => {
    result({ data: { ...TOPIC, status: 'Submitted' } });
    setup('/topics/TOP-2026-014', ['member']);
    expect(screen.queryByRole('button', { name: 'Reclassify' })).toBeNull();
  });

  // ---- WBS-40.2 / DEC-171: the source picker DW-076 carried ----

  it('reclassifies the source alone, carrying the existing type unchanged', async () => {
    // Mirror of the type-only test: the type sent back must be the topic's own, not a default.
    result({ data: { ...TOPIC, status: 'Submitted', type: 'GovernanceStandardization' } });
    setup();
    await userEvent.click(screen.getByRole('button', { name: 'Reclassify' }));
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Source' }), 'Regulatory');
    await userEvent.click(screen.getByRole('button', { name: 'Apply changes' }));
    expect(reclassifyMutate.mock.calls[0][0]).toEqual({
      topicId: 'g1', type: 'GovernanceStandardization', source: 'Regulatory',
    });
  });

  it('opens on the current type and source, with the confirm disabled until one changes', async () => {
    // The server treats an unchanged pair as a no-op, so an enabled confirm would do nothing.
    result({ data: { ...TOPIC, status: 'Submitted', source: 'SecurityFinding' } });
    setup();
    await userEvent.click(screen.getByRole('button', { name: 'Reclassify' }));
    const type = screen.getByRole('combobox', { name: 'Type' }) as HTMLSelectElement;
    const source = screen.getByRole('combobox', { name: 'Source' }) as HTMLSelectElement;
    expect(type.value).toBe('ArchitectureDecision');
    expect(source.value).toBe('SecurityFinding');
    // Every value is offered, the current one included — it is the pre-selected option.
    expect(type.options).toHaveLength(4);
    expect(source.options).toHaveLength(10);
    const confirm = screen.getByRole('button', { name: 'Apply changes' });
    expect(confirm).toBeDisabled();
    await userEvent.selectOptions(source, 'External');
    expect(confirm).toBeEnabled();
    await userEvent.selectOptions(source, 'SecurityFinding');
    expect(confirm).toBeDisabled();
  });

  it('shows the topic source on the overview, in its glossary label', () => {
    result({ data: { ...TOPIC, source: 'CrossStreamProblem' } });
    setup();
    expect(screen.getByText('Cross-stream problem')).toBeInTheDocument();
  });
});
