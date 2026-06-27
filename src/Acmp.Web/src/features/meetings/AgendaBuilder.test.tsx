import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import axe from 'axe-core';
import { AgendaBuilder } from './AgendaBuilder';
import { AcmpAuthContext } from '../../auth/AcmpAuthContext';
import { makeAuth } from '../../test/render';
import type { MeetingDetail } from '../../api/meetings';
import type { Member } from '../../api/members';
import type { PagedResult, TopicSummary } from '../../api/topics';

vi.mock('../../api/meetings', () => ({
  useMeetingDetail: vi.fn(),
  usePreparedTopics: vi.fn(),
  useAddAgendaItem: vi.fn(),
  useRemoveAgendaItem: vi.fn(),
  useMoveAgendaItem: vi.fn(),
  useSetTimebox: vi.fn(),
  useAssignPresenter: vi.fn(),
  usePublishAgenda: vi.fn(),
}));
vi.mock('../../api/members', () => ({ useMembers: vi.fn() }));

import {
  useMeetingDetail,
  usePreparedTopics,
  useAddAgendaItem,
  useRemoveAgendaItem,
  useMoveAgendaItem,
  useSetTimebox,
  useAssignPresenter,
  usePublishAgenda,
} from '../../api/meetings';
import { useMembers } from '../../api/members';

const mockDetail = useMeetingDetail as unknown as Mock;
const mockPrepared = usePreparedTopics as unknown as Mock;
const mockMembers = useMembers as unknown as Mock;

let addSpy: Mock, removeSpy: Mock, moveSpy: Mock, timeboxSpy: Mock, presenterSpy: Mock, publishSpy: Mock;

const MEETING: MeetingDetail = {
  id: 'm1', key: 'MTG-2026-019', title: 'Q2 Architecture Review', committeeId: 'c1',
  scheduledStart: '2026-06-30T09:00:00Z', scheduledEnd: '2026-06-30T10:30:00Z', // 90 min
  status: 'Scheduled', location: null, joinUrl: null, chairUserId: 'u1', chairName: 'Sara K', startedAt: null, heldAt: null,
  agenda: {
    id: 'a1', key: 'AGD-2026-019', status: 'Draft', version: 1, totalTimeboxMinutes: 35, publishedAt: null,
    items: [
      { topicId: 't1', topicKey: 'TOP-2026-014', topicTitle: 'Adopt Keycloak', urgent: true, order: 0, timeboxMinutes: 20, presenterUserId: null, presenterName: null, outcome: 'Pending', actualMinutes: 0 },
      { topicId: 't2', topicKey: 'TOP-2026-031', topicTitle: 'Event streaming spike', urgent: false, order: 1, timeboxMinutes: 15, presenterUserId: null, presenterName: null, outcome: 'Pending', actualMinutes: 0 },
    ],
  },
  attendance: [], discussions: [],
};

const PREPARED: PagedResult<TopicSummary> = {
  items: [
    {
      id: 't3', key: 'TOP-2026-040', title: 'Adopt OpenTelemetry', type: 'ArchitectureDecision', status: 'Prepared',
      urgency: 'Normal', scope: 'MultiStream', streams: [], ownerId: null, ownerName: null, priority: 1, ageDays: 2, slaBreached: false, createdAt: '2026-06-01T09:00:00Z',
    },
  ],
  total: 1, page: 1, pageSize: 200, totalPages: 1,
};

const MEMBERS: Member[] = [
  { publicId: 'u9', fullName: 'Lina M', email: 'lina@example.com', role: 'Member', status: 'Active', isActive: true, isVotingEligible: true, streams: [] },
];

function detailResult(over: Partial<ReturnType<typeof useMeetingDetail>>) {
  mockDetail.mockReturnValue({ data: undefined, isLoading: false, isError: false, error: null, refetch: vi.fn(), ...over });
}

function setup(path = '/meetings/MTG-2026-019') {
  render(
    <AcmpAuthContext.Provider value={makeAuth(['secretary'])}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/meetings/:key" element={<AgendaBuilder />} />
        </Routes>
      </MemoryRouter>
    </AcmpAuthContext.Provider>,
  );
}

describe('AgendaBuilder (P6c)', () => {
  beforeEach(() => {
    [mockDetail, mockPrepared, mockMembers].forEach((m) => m.mockReset());
    addSpy = vi.fn();
    removeSpy = vi.fn();
    moveSpy = vi.fn();
    timeboxSpy = vi.fn();
    presenterSpy = vi.fn();
    publishSpy = vi.fn();
    (useAddAgendaItem as unknown as Mock).mockReturnValue({ mutate: addSpy, isPending: false });
    (useRemoveAgendaItem as unknown as Mock).mockReturnValue({ mutate: removeSpy, isPending: false });
    (useMoveAgendaItem as unknown as Mock).mockReturnValue({ mutate: moveSpy, isPending: false });
    (useSetTimebox as unknown as Mock).mockReturnValue({ mutate: timeboxSpy, isPending: false });
    (useAssignPresenter as unknown as Mock).mockReturnValue({ mutate: presenterSpy, isPending: false });
    (usePublishAgenda as unknown as Mock).mockReturnValue({ mutate: publishSpy, isPending: false });
    mockPrepared.mockReturnValue({ data: PREPARED, isLoading: false });
    mockMembers.mockReturnValue({ data: MEMBERS });
  });

  it('renders the header, draft chip, budget, pool, and agenda items', () => {
    detailResult({ data: MEETING });
    setup();
    expect(screen.getByRole('heading', { name: 'Q2 Architecture Review' })).toBeInTheDocument();
    expect(screen.getByText('Draft')).toBeInTheDocument();
    expect(screen.getByText('Time budget')).toBeInTheDocument();
    // Budget: 35 used of 90 → 55 buffer, comfortably "Fits".
    expect(screen.getByText('55 min buffer')).toBeInTheDocument();
    expect(screen.getByText('Fits')).toBeInTheDocument();
    expect(screen.getByText('Adopt OpenTelemetry')).toBeInTheDocument(); // pool (Prepared)
    expect(screen.getByText('Adopt Keycloak')).toBeInTheDocument(); // agenda item
  });

  it('adds a topic from the pool by topic id (AC: place onto agenda)', async () => {
    detailResult({ data: MEETING });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Add TOP-2026-040 to the agenda' }));
    expect(addSpy).toHaveBeenCalledWith(
      expect.objectContaining({ meetingId: 'm1', topicId: 't3', topicKey: 'TOP-2026-040', timeboxMinutes: 15 }),
    );
  });

  it('moves an item down with a +1 delta and announces it (AC-044)', async () => {
    detailResult({ data: MEETING });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Move TOP-2026-014 down' }));
    expect(moveSpy).toHaveBeenCalledWith({ meetingId: 'm1', topicId: 't1', delta: 1 });
    expect(screen.getByText('TOP-2026-014 moved down.')).toBeInTheDocument();
  });

  it('disables move-up on the first item and move-down on the last', () => {
    detailResult({ data: MEETING });
    setup();
    expect(screen.getByRole('button', { name: 'Move TOP-2026-014 up' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Move TOP-2026-031 down' })).toBeDisabled();
  });

  it('decrements the timebox by the step', async () => {
    detailResult({ data: MEETING });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Decrease timebox for TOP-2026-014' }));
    expect(timeboxSpy).toHaveBeenCalledWith({ meetingId: 'm1', topicId: 't1', minutes: 15 });
  });

  it('removes an item by topic id', async () => {
    detailResult({ data: MEETING });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Remove TOP-2026-014 from the agenda' }));
    expect(removeSpy).toHaveBeenCalledWith({ meetingId: 'm1', topicId: 't1' });
  });

  it('opens the publish dialog and publishes by meeting id', async () => {
    detailResult({ data: MEETING });
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'Publish & notify' }));
    const dialog = screen.getByRole('dialog');
    expect(within(dialog).getByText('All committee members will be notified.')).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Publish & notify' }));
    expect(publishSpy).toHaveBeenCalledWith({ meetingId: 'm1' }, expect.anything());
  });

  it('shows the empty agenda state and disables publish when there are no items', () => {
    detailResult({ data: { ...MEETING, agenda: { ...MEETING.agenda!, items: [], totalTimeboxMinutes: 0 } } });
    setup();
    expect(screen.getByText('No items yet')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Publish & notify' })).toBeDisabled();
  });

  it('shows the loading state while fetching', () => {
    detailResult({ isLoading: true });
    setup();
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('shows a not-found state for a 404 meeting', async () => {
    const { ApiError } = await import('../../api/apiClient');
    detailResult({ isError: true, error: new ApiError(404) });
    setup('/meetings/MTG-9999-999');
    expect(screen.getByText('Meeting not found')).toBeInTheDocument();
  });

  it('is axe-clean (WCAG 2.2 AA structure/ARIA)', async () => {
    detailResult({ data: MEETING });
    setup();
    const results = await axe.run(document.body, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(results.violations.map((v) => v.id)).toEqual([]);
  });
});
