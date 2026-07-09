import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { render, screen } from '@testing-library/react';
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
vi.mock('../../api/topics', () => ({ useTopicDetail: vi.fn(), useAddTopicComment: vi.fn(), useUploadTopicAttachment: vi.fn(), usePrepareTopic: vi.fn() }));
import { useTopicDetail, useAddTopicComment, useUploadTopicAttachment, usePrepareTopic } from '../../api/topics';

const mockDetail = useTopicDetail as unknown as Mock;
const mockAddComment = useAddTopicComment as unknown as Mock;
const mockUpload = useUploadTopicAttachment as unknown as Mock;
const mockPrepare = usePrepareTopic as unknown as Mock;
let mutate: Mock;
let uploadMutate: Mock;
let prepareMutate: Mock;

const TOPIC: Topic = {
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
