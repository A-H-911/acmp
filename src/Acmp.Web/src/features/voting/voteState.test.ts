import { describe, it, expect } from 'vitest';
import { deriveVoteContext, hasCast, optionTone } from './voteState';
import type { Ballot, VoteDetail, VoteStatus } from '../../api/votes';

function ballot(over: Partial<Ballot> = {}): Ballot {
  return { voterUserId: 'me', voterName: 'Me', choice: null, comment: null, recused: false, castAt: null, ...over };
}

function vote(status: VoteStatus, ballots: Ballot[]): VoteDetail {
  return {
    id: 'g1', key: 'VOTE-2026-014', topicId: 't1', meetingId: 'm1', status,
    options: ['Approve', 'Reject'], allowAbstain: true, minPresent: 5, minCast: 5,
    tally: null, resultSummary: null, openedAt: null, closedAt: null,
    counterUserId: null, counterName: null, ballots,
  };
}

describe('deriveVoteContext', () => {
  it('Configured → not_open regardless of ballots', () => {
    expect(deriveVoteContext(vote('Configured', [ballot()]), 'me').view).toBe('not_open');
  });

  it('Open + my eligible, uncast ballot → open (first cast)', () => {
    const ctx = deriveVoteContext(vote('Open', [ballot()]), 'me');
    expect(ctx.view).toBe('open');
    expect(hasCast(ctx.myBallot)).toBe(false);
    expect(ctx.isOpen).toBe(true);
  });

  it('Open + my ballot already cast → still open (editable until close, Fork 1)', () => {
    const ctx = deriveVoteContext(vote('Open', [ballot({ choice: 'Approve', castAt: 'x' })]), 'me');
    expect(ctx.view).toBe('open');
    expect(hasCast(ctx.myBallot)).toBe(true);
  });

  it('Open + no ballot row for me → ineligible (view-only)', () => {
    expect(deriveVoteContext(vote('Open', [ballot({ voterUserId: 'other' })]), 'me').view).toBe('ineligible');
  });

  it('Open + my ballot recused → recused', () => {
    expect(deriveVoteContext(vote('Open', [ballot({ recused: true })]), 'me').view).toBe('recused');
  });

  it('Closed and Ratified → closed (frozen), even for an eligible voter', () => {
    expect(deriveVoteContext(vote('Closed', [ballot()]), 'me').view).toBe('closed');
    const r = deriveVoteContext(vote('Ratified', [ballot()]), 'me');
    expect(r.view).toBe('closed');
    expect(r.isClosed).toBe(true);
  });

  it('undefined userId → no ballot, so an Open vote reads as ineligible', () => {
    const ctx = deriveVoteContext(vote('Open', [ballot()]), undefined);
    expect(ctx.myBallot).toBeNull();
    expect(ctx.view).toBe('ineligible');
  });
});

describe('optionTone', () => {
  it('maps the fixed option set to the design palette', () => {
    expect(optionTone('Approve')).toBe('success');
    expect(optionTone('Reject')).toBe('danger');
    expect(optionTone('Abstain')).toBe('neutral');
    expect(optionTone('Whatever')).toBe('neutral');
  });
});
