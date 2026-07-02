import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { VotePage } from './VotePage';
import { AcmpAuthContext } from '../../auth/AcmpAuthContext';
import { makeAuth } from '../../test/render';
import { ApiError } from '../../api/apiClient';
import type { Ballot, VoteDetail, VoteStatus } from '../../api/votes';

vi.mock('../../api/votes', async (orig) => ({
  ...(await orig<typeof import('../../api/votes')>()),
  useVote: vi.fn(),
  useOpenVote: vi.fn(),
  useCastBallot: vi.fn(),
  useChangeBallot: vi.fn(),
  useRecuseVote: vi.fn(),
  useCloseVote: vi.fn(),
}));
import {
  useVote, useOpenVote, useCastBallot, useChangeBallot, useRecuseVote, useCloseVote,
} from '../../api/votes';

const mockVote = useVote as unknown as Mock;
const muts = { open: useOpenVote, cast: useCastBallot, change: useChangeBallot, recuse: useRecuseVote, close: useCloseVote } as unknown as Record<string, Mock>;
const fns: Record<string, Mock> = {};

function mutation(name: string, impl?: () => Promise<unknown>) {
  const fn = vi.fn(impl ?? (() => Promise.resolve(undefined)));
  fns[name] = fn;
  return { mutateAsync: fn, isPending: false };
}

function ballot(over: Partial<Ballot> = {}): Ballot {
  return { voterUserId: 'me', voterName: 'Me Member', choice: null, comment: null, recused: false, castAt: null, ...over };
}

function vote(status: VoteStatus, over: Partial<VoteDetail> = {}): VoteDetail {
  return {
    id: 'g1', key: 'VOTE-2026-014', topicId: 't1', meetingId: 'm1', status,
    options: ['Approve', 'Reject'], allowAbstain: true, minPresent: 2, minCast: 2,
    tally: null, resultSummary: null, openedAt: null, closedAt: null,
    counterUserId: null, counterName: null, ballots: [ballot()], ...over,
  };
}

function setVote(over: Partial<ReturnType<typeof useVote>>) {
  mockVote.mockReturnValue({ data: undefined, isLoading: false, isError: false, error: null, refetch: vi.fn(), ...over });
}

function setup(userId = 'me', roles: Parameters<typeof makeAuth>[0] = ['member']) {
  render(
    <AcmpAuthContext.Provider value={makeAuth(roles, { userId })}>
      <MemoryRouter initialEntries={['/votes/VOTE-2026-014']}>
        <Routes>
          <Route path="/votes/:key" element={<VotePage />} />
        </Routes>
      </MemoryRouter>
    </AcmpAuthContext.Provider>,
  );
}

describe('VotePage (P9b)', () => {
  beforeEach(() => {
    mockVote.mockReset();
    muts.open.mockReturnValue(mutation('open'));
    muts.cast.mockReturnValue(mutation('cast'));
    muts.change.mockReturnValue(mutation('change'));
    muts.recuse.mockReturnValue(mutation('recuse'));
    muts.close.mockReturnValue(mutation('close'));
  });

  it('shows loading then not-found', () => {
    setVote({ isLoading: true });
    setup();
    expect(screen.getByRole('status')).toBeInTheDocument();

    setVote({ isError: true, error: new ApiError(404, { title: 'x' }) });
    setup();
    expect(screen.getByText('Vote not found')).toBeInTheDocument();
  });

  it('renders the header, key, attributed badge and eligible voters', () => {
    setVote({ data: vote('Open') });
    setup();
    expect(screen.getByRole('heading', { name: 'Vote' })).toBeInTheDocument();
    expect(screen.getByText('VOTE-2026-014')).toBeInTheDocument();
    expect(screen.getByText('Attributed (not anonymous)')).toBeInTheDocument();
    expect(screen.getByText('Voting open')).toBeInTheDocument();
    expect(screen.getByText('Me Member')).toBeInTheDocument();
    expect(screen.getByText('Awaiting')).toBeInTheDocument();
  });

  it('not_open: a manager sees Open voting and can open the vote', async () => {
    const user = userEvent.setup();
    setVote({ data: vote('Configured') });
    setup('me', ['chairman']);
    expect(screen.getByText('Voting not yet open')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Open voting/ }));
    expect(fns.open).toHaveBeenCalledWith({ id: 'g1' });
  });

  it('not_open: a non-manager sees no Open voting button', () => {
    setVote({ data: vote('Configured') });
    setup('me', ['member']);
    expect(screen.queryByRole('button', { name: /Open voting/ })).not.toBeInTheDocument();
  });

  it('open: an eligible voter casts a ballot through the confirm dialog', async () => {
    const user = userEvent.setup();
    setVote({ data: vote('Open') });
    setup();
    await user.click(screen.getByRole('radio', { name: 'Approve' }));
    await user.click(screen.getByRole('button', { name: /Cast vote/ }));
    // confirm dialog
    await user.click(screen.getByRole('button', { name: 'Confirm & cast' }));
    expect(fns.cast).toHaveBeenCalledWith({ id: 'g1', choice: 'Approve', comment: null });
  });

  it('open: a previously-cast ballot shows the recorded choice and uses change', async () => {
    const user = userEvent.setup();
    setVote({ data: vote('Open', { ballots: [ballot({ choice: 'Reject', castAt: '2026-02-18T10:00:00Z' })] }) });
    setup();
    expect(screen.getByText('Your recorded vote')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Change vote/ }));
    await user.click(screen.getByRole('button', { name: 'Confirm & cast' }));
    expect(fns.change).toHaveBeenCalledWith({ id: 'g1', choice: 'Reject', comment: null });
  });

  it('open: recuse posts the recusal', async () => {
    const user = userEvent.setup();
    setVote({ data: vote('Open') });
    setup();
    await user.click(screen.getByRole('button', { name: /Recuse/ }));
    expect(fns.recuse).toHaveBeenCalledWith({ id: 'g1' });
  });

  it('open: a manager can close, and a quorum-failure close surfaces an error', async () => {
    const user = userEvent.setup();
    muts.close.mockReturnValue({ mutateAsync: vi.fn(() => Promise.reject(new ApiError(409, { title: 'q' }))), isPending: false });
    setVote({ data: vote('Open') });
    setup('me', ['chairman']);
    await user.click(screen.getByRole('button', { name: /Close voting/ }));
    expect(await screen.findByRole('alert')).toHaveTextContent(/could not be closed/);
  });

  it('ineligible: a non-voter sees the view-only card', () => {
    setVote({ data: vote('Open', { ballots: [ballot({ voterUserId: 'other', voterName: 'Other' })] }) });
    setup('me');
    expect(screen.getByText('View-only')).toBeInTheDocument();
  });

  it('recused: the current voter sees the recused card', () => {
    setVote({ data: vote('Open', { ballots: [ballot({ recused: true })] }) });
    setup();
    expect(screen.getByText('You are recused')).toBeInTheDocument();
  });

  it('closed & ratified: shows the frozen tally, result, counter and ratified note', () => {
    setVote({
      data: vote('Ratified', {
        closedAt: '2026-02-18T10:34:00Z', counterName: 'Khalid A.', resultSummary: 'Approve: 2, Reject: 0',
        tally: { optionCounts: { Approve: 2, Reject: 0 }, abstainCount: 0, castCount: 2 },
        ballots: [ballot({ choice: 'Approve', castAt: 'x' }), ballot({ voterUserId: 'o', voterName: 'Omar', choice: 'Approve', castAt: 'x' })],
      }),
    });
    setup();
    expect(screen.getByText('Closed & locked')).toBeInTheDocument();
    expect(screen.getByText('Vote closed & locked')).toBeInTheDocument();
    expect(screen.getByText('Approve: 2, Reject: 0')).toBeInTheDocument();
    expect(screen.getByText(/Khalid A\./)).toBeInTheDocument();
    expect(screen.getByText('Ratified into a decision')).toBeInTheDocument();
  });
});
