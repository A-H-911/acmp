import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { CallVoteDialog } from './CallVoteDialog';
import type { Member } from '../../api/members';

const navigate = vi.fn();
vi.mock('react-router-dom', async (orig) => ({ ...(await orig<typeof import('react-router-dom')>()), useNavigate: () => navigate }));

vi.mock('../../api/members', () => ({ useMembers: vi.fn() }));
vi.mock('../../api/votes', () => ({ useConfigureVote: vi.fn() }));
import { useMembers } from '../../api/members';
import { useConfigureVote } from '../../api/votes';

const mockMembers = useMembers as unknown as Mock;
const mockConfigure = useConfigureVote as unknown as Mock;
let mutateAsync: Mock;

function member(over: Partial<Member>): Member {
  return {
    publicId: 'p', keycloakUserId: 'kc', fullName: 'X', email: 'x@a.co', role: 'Member',
    status: 'Active', isActive: true, isVotingEligible: true, streams: [], ...over,
  };
}

const ELIGIBLE: Member[] = [
  member({ publicId: 'p1', keycloakUserId: 'kc-1', fullName: 'Sara M.', role: 'Chairman' }),
  member({ publicId: 'p2', keycloakUserId: 'kc-2', fullName: 'Omar H.' }),
  member({ publicId: 'p3', keycloakUserId: 'kc-3', fullName: 'Noura P.', isVotingEligible: false }), // excluded
];

function setup() {
  render(
    <MemoryRouter>
      <CallVoteDialog open onClose={vi.fn()} source={{ topicId: 't1', topicKey: 'TOP-2026-014', meetingId: 'm1' }} />
    </MemoryRouter>,
  );
}

describe('CallVoteDialog (P9b)', () => {
  beforeEach(() => {
    navigate.mockReset();
    mutateAsync = vi.fn(() => Promise.resolve({ id: 'g9', key: 'VOTE-2026-015' }));
    mockConfigure.mockReturnValue({ mutateAsync, isPending: false });
    mockMembers.mockReturnValue({ data: ELIGIBLE });
  });

  it('locks the topic and lists only voting-eligible active members', () => {
    setup();
    expect(screen.getByText('TOP-2026-014')).toBeInTheDocument();
    expect(screen.getByText(/Sara M\./)).toBeInTheDocument();
    expect(screen.getByText(/Omar H\./)).toBeInTheDocument();
    expect(screen.queryByText(/Noura P\./)).not.toBeInTheDocument();
  });

  it('validates the quorum against the eligible-voter count', async () => {
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByRole('spinbutton'), '9'); // > 2 eligible
    await user.click(screen.getByRole('button', { name: 'Configure vote' }));
    expect(await screen.findByText(/Quorum must be between/)).toBeInTheDocument();
    expect(mutateAsync).not.toHaveBeenCalled();
  });

  it('configures the vote and navigates to the new /votes/:key', async () => {
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByRole('spinbutton'), '2');
    await user.click(screen.getByRole('button', { name: 'Configure vote' }));
    expect(mutateAsync).toHaveBeenCalledWith(expect.objectContaining({
      topicId: 't1', meetingId: 'm1', options: ['Approve', 'Reject'], allowAbstain: true, minPresent: 2, minCast: 2,
    }));
    const arg = mutateAsync.mock.calls[0][0] as { eligibleVoters: { userId: string }[] };
    expect(arg.eligibleVoters.map((v) => v.userId)).toEqual(['kc-1', 'kc-2']); // default = all eligible
    expect(navigate).toHaveBeenCalledWith('/votes/VOTE-2026-015');
  });

  it('blocks configure when fewer than two voters are selected', async () => {
    const user = userEvent.setup();
    setup();
    // Deselect one of the two eligible → only one remains
    await user.click(screen.getAllByRole('checkbox')[0]);
    await user.type(screen.getByRole('spinbutton'), '1');
    await user.click(screen.getByRole('button', { name: 'Configure vote' }));
    expect(await screen.findByText(/at least two eligible voters/)).toBeInTheDocument();
    expect(mutateAsync).not.toHaveBeenCalled();
  });
});
