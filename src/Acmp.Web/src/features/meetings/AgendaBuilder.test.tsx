import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { render, screen, within, fireEvent } from '@testing-library/react';
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
  // FR-159 — the row now carries a guest-presenter invite for a Secretary. Mocked here because
  // this file mocks the whole api module; the invite's own behaviour is proven in
  // GuestPresenterInvite.test.tsx against a real hook and a stubbed fetch.
  useInviteGuestPresenter: vi.fn(() => ({ mutate: vi.fn(), reset: vi.fn(), isPending: false, isError: false })),
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
  status: 'Scheduled', type: 'Regular', mode: 'InPerson', location: null, joinUrl: null, chairUserId: 'u1', chairName: 'Sara K', startedAt: null, heldAt: null,
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
      restricted: false,
      id: 't3', key: 'TOP-2026-040', title: 'Adopt OpenTelemetry', type: 'ArchitectureDecision', status: 'Prepared',
      urgency: 'Normal', scope: 'MultiStream', streams: [], ownerId: null, ownerName: null, priority: 1, timesDeferred: 0, ageDays: 2, slaBreached: false, createdAt: '2026-06-01T09:00:00Z',
    },
  ],
  total: 1, page: 1, pageSize: 200, totalPages: 1,
};

const MEMBERS: Member[] = [
  { publicId: 'u9', keycloakUserId: 'kc-fixture', fullName: 'Lina M', email: 'lina@example.com', role: 'Member', status: 'Active', isActive: true, isVotingEligible: true, streams: [] },
];

function detailResult(over: Partial<ReturnType<typeof useMeetingDetail>>) {
  mockDetail.mockReturnValue({ data: undefined, isLoading: false, isError: false, error: null, refetch: vi.fn(), ...over });
}

function setup(path = '/meetings/MTG-2026-019') {
  return render(
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
    // F-3: the meeting-title H1 is owned by the MeetingPage shell (this tab renders in its
    // Outlet), so the builder no longer renders a duplicate title heading of its own.
    expect(screen.queryByRole('heading', { name: 'Q2 Architecture Review' })).not.toBeInTheDocument();
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

  // The opposite arm of every paired control. Each pair is two call sites with opposite signs, so
  // one working proves nothing about the other - and each writes to the agenda a committee runs from.
  it('moves an item up with a -1 delta', async () => {
    detailResult({ data: MEETING });
    const user = userEvent.setup();
    setup();

    await user.click(screen.getByRole('button', { name: 'Move TOP-2026-031 up' }));

    expect(moveSpy).toHaveBeenCalledWith({ meetingId: 'm1', topicId: 't2', delta: -1 });
  });

  it('increments the timebox by the step', async () => {
    detailResult({ data: MEETING });
    const user = userEvent.setup();
    setup();

    await user.click(screen.getByRole('button', { name: 'Increase timebox for TOP-2026-014' }));

    expect(timeboxSpy).toHaveBeenCalledWith({ meetingId: 'm1', topicId: 't1', minutes: 25 });
  });

  // Assigning a presenter is the one edit here that resolves an id against the member list before
  // committing - so the NAME it snapshots has to come from that lookup, not from the picker's label.
  it('assigns a presenter, snapshotting the resolved member name', async () => {
    detailResult({ data: MEETING });
    const user = userEvent.setup();
    setup();

    await user.click(screen.getByRole('button', { name: 'Presenter for TOP-2026-014' }));
    await user.click(screen.getByRole('option', { name: 'Lina M' }));

    expect(presenterSpy).toHaveBeenCalledWith(
      expect.objectContaining({ meetingId: 'm1', topicId: 't1', presenterUserId: 'u9', presenterName: 'Lina M' }),
    );
  });

  it('filters the prepared-topic pool from the search box', async () => {
    detailResult({ data: MEETING });
    const user = userEvent.setup();
    setup();
    expect(screen.getByRole('button', { name: 'Add TOP-2026-040 to the agenda' })).toBeInTheDocument();

    await user.type(screen.getByRole('searchbox'), 'zzz');

    expect(screen.queryByRole('button', { name: 'Add TOP-2026-040 to the agenda' })).not.toBeInTheDocument();
  });

  // Publishing notifies every committee member and locks the agenda; backing out of that confirm
  // had never run, which makes the dialog a one-way door in practice.
  it('cancels the publish confirmation without publishing', async () => {
    detailResult({ data: MEETING });
    const user = userEvent.setup();
    setup();

    await user.click(screen.getByRole('button', { name: 'Publish & notify' }));
    const dialog = screen.getByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(publishSpy).not.toHaveBeenCalled();
  });

  it('retries a failed meeting fetch from the error state', async () => {
    const { ApiError } = await import('../../api/apiClient');
    const refetch = vi.fn();
    detailResult({ isError: true, error: new ApiError(500), refetch });
    setup();

    await userEvent.click(screen.getByRole('button', { name: /retry|try again/i }));

    expect(refetch).toHaveBeenCalled();
  });

  /*
   * Drag-and-drop is the agenda's PRIMARY gesture and none of its handlers had ever run: dropping a
   * pooled topic onto the agenda, reordering by dropping one item on another, and the dragOver that
   * makes either possible. jsdom cannot judge whether the targets LOOK droppable — that is the
   * e2e's job — but the HTML5 DnD API is plain events, so the handlers a browser would call are
   * exactly these.
   */
  it('adds a pooled topic by dropping it onto the agenda', () => {
    detailResult({ data: MEETING });
    const { container } = setup();
    const pooled = screen.getByRole('button', { name: 'Add TOP-2026-040 to the agenda' }).closest('[draggable]')!;
    const list = container.querySelector('.mt-agenda-list') ?? container.querySelector('[class*="agenda"]')!;

    fireEvent.dragStart(pooled);
    expect(fireEvent.dragOver(list)).toBe(false); // prevented default = a drop is allowed to land
    fireEvent.drop(list);

    expect(addSpy).toHaveBeenCalledWith(
      expect.objectContaining({ meetingId: 'm1', topicId: 't3', topicKey: 'TOP-2026-040' }),
    );
  });

  it('reorders by dropping one agenda item onto another, and ignores a drop on itself', () => {
    detailResult({ data: MEETING });
    setup();
    const first = screen.getByRole('button', { name: 'Move TOP-2026-014 up' }).closest('[draggable]')!;
    const second = screen.getByRole('button', { name: 'Move TOP-2026-031 up' }).closest('[draggable]')!;

    // Self-drop first: the guard is `src.topicId !== target.topicId`, and a reorder fired by a
    // gesture that went nowhere would renumber the agenda for no reason.
    fireEvent.dragStart(second);
    fireEvent.drop(second);
    expect(moveSpy).not.toHaveBeenCalled();

    fireEvent.dragStart(second);
    fireEvent.drop(first);

    // second.order (1) > first.order (0) → moving up, delta -1.
    expect(moveSpy).toHaveBeenCalledWith({ meetingId: 'm1', topicId: 't2', delta: -1 });
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

// The viewer (read-only) mode renders once the agenda is published or the meeting has
// started/concluded/cancelled — AgendaBuilder derives this from the meeting status (beforeEach sets
// Published). This is the fix for the bug where a started meeting still exposed an editable agenda
// builder. Asserts every edit affordance is gone.
describe('AgendaBuilder — read-only viewer', () => {
  beforeEach(() => {
    [mockDetail, mockPrepared, mockMembers].forEach((m) => m.mockReset());
    // The mutation hooks still run (they're unconditional) — return inert handles.
    (useAddAgendaItem as unknown as Mock).mockReturnValue({ mutate: vi.fn(), isPending: false });
    (useRemoveAgendaItem as unknown as Mock).mockReturnValue({ mutate: vi.fn(), isPending: false });
    (useMoveAgendaItem as unknown as Mock).mockReturnValue({ mutate: vi.fn(), isPending: false });
    (useSetTimebox as unknown as Mock).mockReturnValue({ mutate: vi.fn(), isPending: false });
    (useAssignPresenter as unknown as Mock).mockReturnValue({ mutate: vi.fn(), isPending: false });
    (usePublishAgenda as unknown as Mock).mockReturnValue({ mutate: vi.fn(), isPending: false });
    mockPrepared.mockReturnValue({ data: PREPARED, isLoading: false });
    mockMembers.mockReturnValue({ data: MEMBERS });
    detailResult({ data: { ...MEETING, agenda: { ...MEETING.agenda!, status: 'Published' } } });
  });

  function setupViewer() {
    render(
      <AcmpAuthContext.Provider value={makeAuth(['secretary'])}>
        <MemoryRouter initialEntries={['/meetings/MTG-2026-019']}>
          <Routes>
            <Route path="/meetings/:key" element={<AgendaBuilder />} />
          </Routes>
        </MemoryRouter>
      </AcmpAuthContext.Provider>,
    );
  }

  it('still lists the agenda items', () => {
    setupViewer();
    expect(screen.getByText('Adopt Keycloak')).toBeInTheDocument();
    expect(screen.getByText('Event streaming spike')).toBeInTheDocument();
  });

  it('renders the design "Agenda preview" card: header meta, topic key (traceability), and presenter fallback', () => {
    setupViewer();
    // Card header = title + "N items · M min" meta (design isOverview ~L263).
    expect(screen.getByRole('heading', { name: 'Agenda' })).toBeInTheDocument();
    expect(screen.getByText('2 items · 35 min')).toBeInTheDocument();
    // Topic key kept on the secondary line — deliberate deviation from the design preview row
    // for traceability of the (becoming-official) record.
    expect(screen.getByText('TOP-2026-014')).toBeInTheDocument();
    // Glanceable row: presenter falls back when unset (both items here).
    expect(screen.getAllByText('No presenter assigned')).toHaveLength(2);
  });

  it('hides every edit affordance: pool, add, move, remove, timebox steppers, presenter picker, publish', () => {
    setupViewer();
    // Pool (and its Add buttons) gone — the Prepared topic is not shown.
    expect(screen.queryByText('Adopt OpenTelemetry')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Add .* to the agenda/ })).not.toBeInTheDocument();
    // Reorder / remove / timebox controls gone.
    expect(screen.queryByRole('button', { name: /Move .* up/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Move .* down/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Remove .* from the agenda/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Increase timebox/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Decrease timebox/ })).not.toBeInTheDocument();
    // Presenter picker (a combobox) and the Publish action gone.
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Publish & notify' })).not.toBeInTheDocument();
  });

  it('shows the real agenda status in the head (Locked → info, never "Draft")', () => {
    // Regression: the head chip was a binary Published/Draft check, so a Locked (or Closed)
    // agenda — both of which render the viewer — mislabelled as "Draft"/warn. Now reuses the
    // shared 4-tone agendaTone helper (#31).
    detailResult({ data: { ...MEETING, status: 'InProgress', agenda: { ...MEETING.agenda!, status: 'Locked' } } });
    setupViewer();
    expect(screen.getByText('Locked').closest('.status-chip')).toHaveClass('info');
    expect(screen.queryByText('Draft')).not.toBeInTheDocument();
  });

  it('is axe-clean in viewer mode', async () => {
    setupViewer();
    const results = await axe.run(document.body, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
      rules: { 'color-contrast': { enabled: false } },
    });
    expect(results.violations.map((v) => v.id)).toEqual([]);
  });
});
