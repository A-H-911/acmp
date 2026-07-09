import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Kanban } from './Kanban';
import { renderWithAuth } from '../../test/render';
import type { TopicSummary } from '../../api/topics';
import type { Member } from '../../api/members';

vi.mock('../../api/topics', () => ({ useAcceptTopic: vi.fn(), useReturnTopic: vi.fn() }));
import { useAcceptTopic, useReturnTopic } from '../../api/topics';
vi.mock('../../api/members', () => ({ useMembers: vi.fn() }));
import { useMembers } from '../../api/members';

const mockAccept = useAcceptTopic as unknown as Mock;
const mockReturn = useReturnTopic as unknown as Mock;
const mockMembers = useMembers as unknown as Mock;
let acceptMutate: Mock;
let returnMutate: Mock;

const row = (over: Partial<TopicSummary>): TopicSummary => ({
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
    mockAccept.mockReturnValue({ mutate: acceptMutate, isPending: false });
    mockReturn.mockReturnValue({ mutate: returnMutate, isPending: false });
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
});
