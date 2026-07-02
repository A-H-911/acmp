/*
 * View-state derivation for the voting screen (P9b). The design file drives its six
 * states (open/closed/not_open/quorum_failed/ineligible/double_error) from a manual
 * toggle; the real screen DERIVES them from three live facts — the vote status, whether
 * the signed-in user has a ballot row (= eligibility, seeded at configure), and whether
 * that ballot has been cast. Two design states don't map onto the shipped backend:
 *   - `double_error` (design blocks a 2nd submission) — backend allows ChangeBallot until
 *     close (Fork 1), so a cast ballot is shown as editable, never a hard block.
 *   - `quorum_failed` (design shows a resting state) — backend has no such status; a failed
 *     close is a 409 with the vote still Open (Fork 2), surfaced as a toast by the page.
 * Kept as a pure function so the mapping is unit-tested independently of rendering.
 */
import type { Ballot, VoteDetail } from '../../api/votes';

export type VoteView = 'not_open' | 'open' | 'ineligible' | 'recused' | 'closed';

export interface VoteContext {
  view: VoteView;
  /** The signed-in user's own ballot row, when they are an eligible voter. */
  myBallot: Ballot | null;
  /** True once the vote is Closed or Ratified — the frozen, read-only state. */
  isClosed: boolean;
  /** True while ballots can still be cast/changed. */
  isOpen: boolean;
}

export function deriveVoteContext(vote: VoteDetail, userId: string | undefined): VoteContext {
  const myBallot = userId ? vote.ballots.find((b) => b.voterUserId === userId) ?? null : null;
  const isClosed = vote.status === 'Closed' || vote.status === 'Ratified';
  const isOpen = vote.status === 'Open';

  const view: VoteView = ((): VoteView => {
    if (isClosed) return 'closed';
    if (vote.status === 'Configured') return 'not_open';
    // Open: eligibility and recusal decide which ballot surface (if any) the user sees.
    if (!myBallot) return 'ineligible';
    if (myBallot.recused) return 'recused';
    return 'open';
  })();

  return { view, myBallot, isClosed, isOpen };
}

/** Whether the user's ballot is a change (already cast) vs a first cast — drives cast|change. */
export function hasCast(ballot: Ballot | null): boolean {
  return !!ballot && ballot.choice !== null;
}

/** Design tally tone: Approve→success, Reject→danger, Abstain/other→neutral (Fork 4 palette). */
export function optionTone(option: string): 'success' | 'danger' | 'neutral' {
  if (option === 'Approve') return 'success';
  if (option === 'Reject') return 'danger';
  return 'neutral';
}
