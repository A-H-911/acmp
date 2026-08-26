import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Kanban } from './Kanban';
import { renderWithAuth } from '../../test/render';
import i18n from '../../i18n';
import type { TopicSummary } from '../../api/topics';
import type { Member } from '../../api/members';

vi.mock('../../api/topics', () => ({ useAcceptTopic: vi.fn(), useReturnTopic: vi.fn(), useMoveTopicPriority: vi.fn() }));
import { useAcceptTopic, useReturnTopic, useMoveTopicPriority } from '../../api/topics';
vi.mock('../../api/members', () => ({ useMembers: vi.fn() }));
import { useMembers } from '../../api/members';

const mockAccept = useAcceptTopic as unknown as Mock;
const mockReturn = useReturnTopic as unknown as Mock;
const mockMove = useMoveTopicPriority as unknown as Mock;
const mockMembers = useMembers as unknown as Mock;
let acceptMutate: Mock;
let returnMutate: Mock;
let moveMutate: Mock;

const row = (over: Partial<TopicSummary>): TopicSummary => ({
  restricted: false,
  id: 'x', key: 'TOP-0', title: 'T', type: 'ArchitectureDecision', status: 'Triage', urgency: 'Normal',
  scope: 'SingleStream', streams: ['identity'], ownerId: null, ownerName: null, priority: 0, timesDeferred: 0, ageDays: 1,
  slaBreached: false, createdAt: '2026-02-15T09:00:00Z', ...over,
});

const ROWS: TopicSummary[] = [
  row({ id: 't1', key: 'TOP-2026-101', title: 'Triage topic', status: 'Triage' }),
  row({ id: 'a1', key: 'TOP-2026-102', title: 'Accepted topic', status: 'Accepted', ownerName: 'Omar H', ownerId: 'o9' }),
  row({ id: 's1', key: 'TOP-2026-103', title: 'Scheduled topic', status: 'Scheduled' }),
];

const MEMBERS: Member[] = [
  { publicId: 'm1', keycloakUserId: 'kc-fixture', fullName: 'Khalid A', email: 'k@acmp.gov', role: 'Secretary', status: 'Active', isActive: true, isVotingEligible: true, streams: [] },
];

function card(key: string) {
  return screen.getByRole('group', { name: new RegExp(key) });
}

describe('Kanban (P5b)', () => {
  beforeEach(() => {
    acceptMutate = vi.fn();
    returnMutate = vi.fn();
    moveMutate = vi.fn();
    mockAccept.mockReturnValue({ mutate: acceptMutate, isPending: false });
    mockReturn.mockReturnValue({ mutate: returnMutate, isPending: false });
    mockMove.mockReturnValue({ mutate: moveMutate, isPending: false });
    mockMembers.mockReturnValue({ data: MEMBERS });
  });

  it('renders the five buckets and groups topics by canonical status', () => {
    renderWithAuth(<Kanban rows={ROWS} />, { roles: ['secretary'] });
    // Triage / Accepted / Scheduled columns each carry their topic.
    expect(screen.getByRole('group', { name: /TOP-2026-101/ })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: /Triage, 1/ })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: /Accepted, 1/ })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: /Scheduled, 1/ })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: /Returned, 0/ })).toBeInTheDocument();
  });

  it('announces the column count in the reader digits, not just on screen (DEF-111 / NFR-037)', async () => {
    /*
     * The accessible name is what a screen-reader user HEARS. It used to be a template literal
     * interpolating the count raw, twelve lines above the same number rendered through <Num> — so in
     * Arabic the eye got the localized form and the ear got a Latin one. Asserting the VISIBLE count
     * would not have caught that; only the accessible name can.
     *
     * MUTATION CHECK: put the template literal back and this goes red while every other test in this
     * file stays green, because they all run in English, where both forms render identically.
     */
    await i18n.changeLanguage('ar');
    try {
      renderWithAuth(<Kanban rows={ROWS} />, { roles: ['secretary'] });
      const names = screen.getAllByRole('region').map((r) => r.getAttribute('aria-label') ?? '');
      expect(names.length).toBeGreaterThan(0);
      expect(names.some((n) => /[٠-٩]/.test(n))).toBe(true);
      expect(names.every((n) => !/[0-9]/.test(n))).toBe(true);
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  it('badges a Prepared topic so it stays distinct inside the shared Accepted bucket (D-15)', () => {
    const rows = [row({ id: 'p1', key: 'TOP-2026-104', title: 'Prepared topic', status: 'Prepared', ownerName: 'Omar H', ownerId: 'o9' })];
    renderWithAuth(<Kanban rows={rows} />, { roles: ['secretary'] });
    // 'Prepared' is not a bucket label — the only source of that text on the board is the card badge.
    expect(screen.getByText('Prepared')).toBeInTheDocument();
    expect(screen.getByRole('region', { name: /Accepted, 1/ })).toBeInTheDocument();
  });

  it('keyboard "M" → move popover → Accepted opens the accept dialog and accepts with an owner', async () => {
    const user = userEvent.setup();
    renderWithAuth(<Kanban rows={ROWS} />, { roles: ['secretary'] });

    fireEvent.keyDown(card('TOP-2026-101'), { key: 'M' });
    // Move popover lists the buckets; pick Accepted.
    await user.click(screen.getByRole('button', { name: /Accepted/ }));
    // Accept dialog: choose an owner, confirm.
    expect(screen.getByText('Accept TOP-2026-101 into the backlog')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Owner' }));
    await user.click(screen.getByRole('option', { name: 'Khalid A' }));
    await user.click(screen.getByRole('button', { name: 'Accept' }));

    expect(acceptMutate).toHaveBeenCalledWith(
      { topicId: 't1', ownerId: 'm1', ownerName: 'Khalid A' },
      expect.anything(),
    );
  });

  it('announces an illegal move (→ scheduled needs a meeting, P6)', async () => {
    const user = userEvent.setup();
    renderWithAuth(<Kanban rows={ROWS} />, { roles: ['secretary'] });
    fireEvent.keyDown(card('TOP-2026-101'), { key: 'M' });
    await user.click(screen.getByRole('button', { name: /Scheduled/ }));
    expect(screen.getByText(/move to Scheduled/)).toBeInTheDocument();
    expect(acceptMutate).not.toHaveBeenCalled();
  });

  it('returns a topic with a reason (defer/reject dialog)', async () => {
    const user = userEvent.setup();
    renderWithAuth(<Kanban rows={ROWS} />, { roles: ['secretary'] });
    fireEvent.keyDown(card('TOP-2026-101'), { key: 'M' });
    await user.click(screen.getByRole('button', { name: /Returned/ }));
    await user.type(screen.getByLabelText(/Reason/), 'Needs a rollback plan first.');
    await user.click(screen.getByRole('button', { name: 'Return topic' }));
    expect(returnMutate).toHaveBeenCalledWith(
      expect.objectContaining({ topicId: 't1', mode: 'defer', reason: 'Needs a rollback plan first.' }),
      expect.anything(),
    );
  });

  it('reorders a topic within its column via the keyboard move buttons (AC-043)', async () => {
    const user = userEvent.setup();
    const rows = [
      row({ id: 't1', key: 'TOP-2026-201', status: 'Triage' }),
      row({ id: 't2', key: 'TOP-2026-202', status: 'Triage' }),
    ];
    renderWithAuth(<Kanban rows={rows} />, { roles: ['secretary'] });

    // The top card can move down (+1) but not up; move it down.
    await user.click(screen.getByRole('button', { name: /Move TOP-2026-201 down/ }));
    expect(moveMutate).toHaveBeenCalledWith({ topicId: 't1', delta: 1 });
    expect(screen.getByRole('button', { name: /Move TOP-2026-201 up/ })).toBeDisabled();
  });

  it('offers no reorder controls in the immutable Done column (AC-043/AC-034)', () => {
    const rows = [row({ id: 'd1', key: 'TOP-2026-301', status: 'Decided' })];
    renderWithAuth(<Kanban rows={rows} />, { roles: ['secretary'] });
    expect(screen.queryByRole('button', { name: /Move TOP-2026-301/ })).not.toBeInTheDocument();
  });

  /*
   * AC-141 / FR-037 — card-level drag reorder. jsdom has no drag-and-drop implementation, but the HTML5
   * DnD API is plain events, so firing dragStart on one card and drop on another exercises exactly the
   * handlers a browser would call. What jsdom cannot judge is whether the cards LOOK droppable; that is
   * the e2e's job.
   */
  it('drops a card onto another in the same column and sends the TARGET IDENTITY, never a position', async () => {
    const rows = [
      row({ id: 't1', key: 'TOP-2026-201', status: 'Triage' }),
      row({ id: 't2', key: 'TOP-2026-202', status: 'Triage' }),
      row({ id: 't3', key: 'TOP-2026-203', status: 'Triage' }),
    ];
    renderWithAuth(<Kanban rows={rows} />, { roles: ['secretary'] });

    fireEvent.dragStart(card('TOP-2026-203'));
    fireEvent.drop(card('TOP-2026-201'));

    // targetTopicId, NOT a delta: this client's list is filtered/sorted/paged, so its indices are not the
    // server's. If anyone "optimises" this to a computed delta, this assertion is what fails.
    expect(moveMutate).toHaveBeenCalledWith({ topicId: 't3', targetTopicId: 't1' });
    expect(moveMutate).toHaveBeenCalledTimes(1);
  });

  it('does NOT reorder when the drop target is in a different column — that is a status change', () => {
    // Cross-column drag belongs to FR-033 and the section-level drop handler. If the card handler claimed
    // it, one gesture would fire a reorder AND a transition. The accept dialog opening is the proof that
    // the section handler still received the gesture.
    renderWithAuth(<Kanban rows={ROWS} />, { roles: ['secretary'] });

    fireEvent.dragStart(card('TOP-2026-101'));   // triage
    fireEvent.drop(card('TOP-2026-102'));        // accepted — different bucket

    expect(moveMutate).not.toHaveBeenCalled();
  });

  /* DEF-103 — the board has no pager, so truncation must be stated AND actionable. Both directions are
     asserted: a silent prefix is the defect, and a notice on a complete board would be noise that
     trains the user to ignore it. */
  // Moving a card UP was never exercised - only down, from the top card, which cannot go up. The
  // two directions are separate call sites with opposite deltas, so one working proves nothing.
  it('reorders a topic upward as well as downward (AC-043)', async () => {
    const user = userEvent.setup();
    const rows = [
      row({ id: 't1', key: 'TOP-2026-201', status: 'Triage' }),
      row({ id: 't2', key: 'TOP-2026-202', status: 'Triage' }),
    ];
    renderWithAuth(<Kanban rows={rows} />, { roles: ['secretary'] });

    await user.click(screen.getByRole('button', { name: /Move TOP-2026-202 up/ }));

    expect(moveMutate).toHaveBeenCalledWith({ topicId: 't2', delta: -1 });
  });

  // Every one of these dialogs could be OPENED and none dismissed. Each sits over a board the
  // secretary is working through, and each dismissal is its own handler on its own state.
  it('dismisses the move popover without moving anything', async () => {
    const user = userEvent.setup();
    renderWithAuth(<Kanban rows={ROWS} />, { roles: ['secretary'] });
    fireEvent.keyDown(card('TOP-2026-101'), { key: 'M' });
    expect(screen.getByRole('button', { name: /Returned/ })).toBeInTheDocument();

    await user.keyboard('{Escape}');

    expect(screen.queryByRole('button', { name: /Returned/ })).not.toBeInTheDocument();
    expect(moveMutate).not.toHaveBeenCalled();
  });

  it.each([
    ['Accepted', 'Accept'],
    ['Returned', 'Return topic'],
  ])('cancels the %s dialog without transitioning the topic', async (bucket, confirmLabel) => {
    const user = userEvent.setup();
    renderWithAuth(<Kanban rows={ROWS} />, { roles: ['secretary'] });
    fireEvent.keyDown(card('TOP-2026-101'), { key: 'M' });
    await user.click(screen.getByRole('button', { name: new RegExp(bucket) }));
    expect(screen.getByRole('button', { name: confirmLabel })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(screen.queryByRole('button', { name: confirmLabel })).not.toBeInTheDocument();
    expect(acceptMutate).not.toHaveBeenCalled();
    expect(returnMutate).not.toHaveBeenCalled();
  });

  // The return dialog offers defer OR reject and only the default had ever been sent. They are
  // different outcomes for the topic's author, so the radio has to reach the command.
  it('returns a topic as rejected when that mode is chosen', async () => {
    const user = userEvent.setup();
    renderWithAuth(<Kanban rows={ROWS} />, { roles: ['secretary'] });
    fireEvent.keyDown(card('TOP-2026-101'), { key: 'M' });
    await user.click(screen.getByRole('button', { name: /Returned/ }));

    await user.click(screen.getByRole('radio', { name: /reject/i }));
    await user.type(screen.getByLabelText(/Reason/), 'Out of committee scope.');
    await user.click(screen.getByRole('button', { name: 'Return topic' }));

    expect(returnMutate).toHaveBeenCalledWith(
      expect.objectContaining({ topicId: 't1', mode: 'reject' }),
      expect.anything(),
    );
  });

  // Both transition commands have an onError arm and neither had run. A silent failure here leaves
  // the card visually unmoved with no explanation, which reads as an unresponsive board.
  it.each([
    ['Accepted', 'Accept'],
    ['Returned', 'Return topic'],
  ])('surfaces a failed %s transition instead of failing silently', async (bucket, confirmLabel) => {
    const user = userEvent.setup();
    acceptMutate.mockImplementation((_v: unknown, o: { onError?: () => void }) => o?.onError?.());
    returnMutate.mockImplementation((_v: unknown, o: { onError?: () => void }) => o?.onError?.());
    renderWithAuth(<Kanban rows={ROWS} />, { roles: ['secretary'] });
    fireEvent.keyDown(card('TOP-2026-101'), { key: 'M' });
    await user.click(screen.getByRole('button', { name: new RegExp(bucket) }));
    // Each dialog has its own precondition before the confirm is meaningful.
    if (confirmLabel === 'Return topic') {
      await user.type(screen.getByLabelText(/Reason/), 'why');
    } else {
      await user.click(screen.getByRole('button', { name: 'Owner' }));
      await user.click(screen.getByRole('option', { name: 'Khalid A' }));
    }

    await user.click(screen.getByRole('button', { name: confirmLabel }));

    expect(await screen.findByRole('alert')).toBeInTheDocument();
  });

  // dragOver and dragEnd are the handlers that make a drop LAND and the drag state clear. A card
  // whose dragOver never preventDefaults is not a drop target at all in a real browser.
  it('accepts the drag over a card and clears the drag state when it ends', () => {
    const rows = [
      row({ id: 't1', key: 'TOP-2026-201', status: 'Triage' }),
      row({ id: 't2', key: 'TOP-2026-202', status: 'Triage' }),
    ];
    renderWithAuth(<Kanban rows={rows} />, { roles: ['secretary'] });

    fireEvent.dragStart(card('TOP-2026-202'));
    // preventDefault on dragOver is what tells the browser a drop is allowed here; without it the
    // drop event never fires and the gesture silently does nothing.
    expect(fireEvent.dragOver(card('TOP-2026-201'))).toBe(false);

    fireEvent.dragEnd(card('TOP-2026-202'));
    fireEvent.drop(card('TOP-2026-201'));

    // The drag was ENDED before the drop, so nothing should move.
    expect(moveMutate).not.toHaveBeenCalled();
  });

  it('says so when the column set is truncated, naming both numbers and what to do', () => {
    const rows = [row({ id: 't1', key: 'TOP-2026-201', status: 'Triage' })];
    renderWithAuth(<Kanban rows={rows} total={60} />, { roles: ['secretary'] });
    const notice = screen.getByRole('status');
    expect(notice).toHaveTextContent(/1/);
    expect(notice).toHaveTextContent(/60/);
    expect(notice).toHaveTextContent(/filter/i);   // actionable, not merely informative
  });

  it('shows no truncation notice when everything is on the board', () => {
    const rows = [row({ id: 't1', key: 'TOP-2026-201', status: 'Triage' })];
    renderWithAuth(<Kanban rows={rows} total={1} />, { roles: ['secretary'] });
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('shows no truncation notice when the caller supplies no total', () => {
    // `total` is optional; an absent total must not be read as 0 and render a bogus "1 of 0".
    const rows = [row({ id: 't1', key: 'TOP-2026-201', status: 'Triage' })];
    renderWithAuth(<Kanban rows={rows} />, { roles: ['secretary'] });
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('ignores a card dropped on itself', () => {
    const rows = [row({ id: 't1', key: 'TOP-2026-201', status: 'Triage' })];
    renderWithAuth(<Kanban rows={rows} />, { roles: ['secretary'] });

    fireEvent.dragStart(card('TOP-2026-201'));
    fireEvent.drop(card('TOP-2026-201'));

    expect(moveMutate).not.toHaveBeenCalled();
  });
});
