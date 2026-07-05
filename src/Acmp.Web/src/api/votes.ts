/*
 * Voting server state (P9b). Wraps GET /api/votes/{key} (detail) and the W11 ballot
 * lifecycle mutations. Mirrors api/decisions.ts: read by key, mutate by Guid id (the
 * detail DTO carries both). Enums travel as string names; the UI localizes them.
 * Optional ballot comments are LocalizedString value objects ({ en, ar }), mirrored
 * (en === ar) like the rest of the app's bilingual free text.
 *
 * The Vote aggregate lives inside the Decisions module (docs/domain/domain-model.md §Vote); its ratification
 * happens as a side-effect of issuing the coupled decision (SoD-3), so there is no
 * "approve vote" mutation here — the closed screen renders the outcome and links out.
 */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './apiClient';

/** Bilingual text as the server serializes LocalizedString (camelCase). */
export interface LocalizedText {
  en: string;
  ar: string;
}

/** VoteStatus (docs/domain/domain-model.md §Vote) — wire = enum names, localized in the UI. Forward-only. */
export type VoteStatus = 'Configured' | 'Open' | 'Closed' | 'Ratified';

export interface Ballot {
  voterUserId: string;
  voterName: string;
  /** null until the voter has cast; 'Abstain' is a first-class choice code. */
  choice: string | null;
  comment: LocalizedText | null;
  recused: boolean;
  castAt: string | null;
}

export interface VoteTally {
  optionCounts: Record<string, number>;
  abstainCount: number;
  castCount: number;
}

export interface VoteDetail {
  id: string;
  key: string;
  topicId: string;
  meetingId: string | null;
  status: VoteStatus;
  options: string[];
  allowAbstain: boolean;
  minPresent: number;
  minCast: number;
  tally: VoteTally | null;
  resultSummary: string | null;
  openedAt: string | null;
  closedAt: string | null;
  counterUserId: string | null;
  counterName: string | null;
  ballots: Ballot[];
}

export function useVote(key: string | undefined) {
  return useQuery({
    queryKey: ['votes', 'detail', key],
    queryFn: () => api<VoteDetail>(`/votes/${key}`),
    enabled: !!key,
    retry: false, // a 404 (unknown key) shouldn't retry — surface "not found" immediately
  });
}

/** Committee-wide votes register row (GET /api/votes). */
export interface VoteSummary {
  id: string;
  key: string;
  topicId: string;
  meetingId: string | null;
  status: VoteStatus;
  options: string[];
  allowAbstain: boolean;
  minPresent: number;
  minCast: number;
  openedAt: string | null;
  closedAt: string | null;
}

/** P12: the committee-wide votes register (no `topic` = across all topics). The chairman dashboard's
 *  "votes awaiting approval" queue passes { status: 'Closed' } (Closed but not yet Ratified). */
export function useVotesRegister(params: { status?: VoteStatus } = {}) {
  const qs = params.status ? `?status=${params.status}` : '';
  return useQuery({
    queryKey: ['votes', 'register', params],
    queryFn: () => api<VoteSummary[]>(`/votes${qs}`),
  });
}

/** An eligible voter seeded at configure time (Keycloak sub + display-name snapshot). */
export interface VoteEligibleVoter {
  userId: string;
  name: string;
}

/** W11 configure (POST /api/votes → 201 + the new VoteSummary). Options are fixed to
 *  Approve/Reject with an optional Abstain (design Fork 4); voters come from the roster. */
export interface ConfigureVoteInput {
  topicId: string;
  meetingId: string;
  options: string[];
  allowAbstain: boolean;
  minPresent: number;
  minCast: number;
  eligibleVoters: VoteEligibleVoter[];
}

export function useConfigureVote() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: ConfigureVoteInput) =>
      api<{ id: string; key: string }>('/votes', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(input),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['votes'] }),
  });
}

/** All in-vote transitions operate by Guid id and refresh the current vote's detail.
 *  The hook takes the read key so it can invalidate exactly that cached detail. */
function useVoteTransition<TVars extends { id: string }>(
  key: string | undefined,
  route: (vars: TVars) => string,
  body?: (vars: TVars) => unknown,
) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (vars: TVars) =>
      api<void>(`/votes/${vars.id}/${route(vars)}`, {
        method: 'POST',
        ...(body
          ? { headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body(vars)) }
          : {}),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['votes', 'detail', key] }),
  });
}

/** W11 open voting (present-quorum enforced server-side → 409 if not met; stays Configured). */
export function useOpenVote(key: string | undefined) {
  return useVoteTransition<{ id: string }>(key, () => 'open');
}

export interface BallotInput {
  id: string;
  choice: string;
  comment: LocalizedText | null;
}

/** W11 cast the first ballot ({ choice, comment }); a second cast is a 409 (AC-022). */
export function useCastBallot(key: string | undefined) {
  return useVoteTransition<BallotInput>(key, () => 'cast', (v) => ({ choice: v.choice, comment: v.comment }));
}

/** W11 change a ballot while the vote is still Open (design's change-until-close). */
export function useChangeBallot(key: string | undefined) {
  return useVoteTransition<BallotInput>(key, () => 'change', (v) => ({ choice: v.choice, comment: v.comment }));
}

/** W11 recuse the current voter (COI) — excluded from the quorum base and the tally. */
export function useRecuseVote(key: string | undefined) {
  return useVoteTransition<{ id: string }>(key, () => 'recuse');
}

/** W11 close voting (cast-quorum enforced → 409 if not met; stays Open). Freezes the tally. */
export function useCloseVote(key: string | undefined) {
  return useVoteTransition<{ id: string }>(key, () => 'close');
}
