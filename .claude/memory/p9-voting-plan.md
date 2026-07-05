---
name: p9-voting-plan
description: P9 Voting phase — P9a backend MERGED; locked decisions; P9b UI is next
metadata: 
  node_type: memory
  type: project
  originSessionId: 5fee7e9b-56ad-4868-ab8e-8b45a4158a32
---

# P9 — Voting

**P9a (Voting backend) COMPLETE & MERGED to main** (squash `3000b79`, PR #74, all 4 CI green, 2026-07-02).
Next slice = **P9b — the voting UI** (`/votes/:key` off `ACMP Decision, Voting & ADR.dc.html` voting screen;
states open/closed/not_open/quorum_failed/ineligible/double_error; ballot / eligible voters / live tally /
quorum pips / COI recusal / chairman approval-override) + wire the meeting-workspace "Call vote" stub. Plan-first,
GO-gated. See [[p8-actions-plan]], [[p7-minutes-decisions-plan]].

## Locked decisions (operator GO)
- **Vote aggregate lives INSIDE the Decisions module** (docs/domain/domain-model.md "Owning module: Decisions") — NOT a new module,
  like MoM lives in Meetings. Same DbContext as Decision, so Vote↔Decision is an in-module read (no seam).
- **Quorum = live attendance-linked (fork 1):** new Shared seam `IMeetingQuorumSource`
  (`Contracts/Meetings`, impl in Meetings.Infrastructure) → present-eligible count (IsVotingEligible +
  Status∈{Present,Late}), checked at **Open** (MinPresent); **cast-count** checked at **Close** (MinCast, AC-024).
  No linked meeting → present check skipped.
- **SoD-3 = Option A (fork 2):** the vote's **closer is the counter of record** (`CounterUserId`). The gate is a
  retrofit in `IssueDecisionHandler` (mirrors P8d's AC-029 retrofit) using the pre-existing
  `SegregationOfDuties.HasIndependentCoAttestation(chair, counter)` → 403 + audited denial if chair==counter;
  ratifies the vote on a clean issue. `VoteId==null` skips the gate (existing P7/P8 paths unchanged).
- **Vote-coupling integrity (from csharp-review HIGH):** a `Decision.VoteId` must exist + be on the SAME topic
  (validated at Record) + be Closed (validated at Issue) — else a dangling/mismatched id silently skips SoD-3.
- **Cast vs ChangeBallot:** `Cast` = first submission only (2nd cast → 409 audited denial, AC-022);
  `ChangeBallot` = design's "change until close". Both under `Vote.Cast` policy.
- **Abstain counts toward the cast-quorum** (deliberate position, ADR-0010); recused ballots don't.
- Lifecycle Configured→Open→Closed→Ratified, forward-only, immutable after Close; **crypto hash-chain deferred
  to P14** (same as Decision/MoM). Keys `VOTE-YYYY-###` (shares `decision_key_counters`). Migration `Votes_Init`.
- Policies pre-existed (P4): `Vote.Manage` (Chairman+Secretary), `Vote.Cast` (Chairman+Member).

## P9b status — DONE & MERGED to main (squash `28d7960`, PR #75, all 4 CI green, 2026-07-02)
FE-only: `api/votes.ts` + `features/voting/{voteState,VotePage,CallVoteDialog}` + voting.css + `/votes/:key`
route + breadcrumb + wired MeetingWorkspace "Call vote" + `voting.*` i18n (EN/AR). 544 FE tests, per-file
≥95% lines. Voting module (P9) COMPLETE (backend P9a + UI P9b). **Next phase = P10 — Risks + Dependencies +
Traceability** (widens AC-029 to typed edges); the deferred issue-from-vote decision path (Fork 3) is
revisited when the record/issue-decision UI is built.

## P9b locked forks (operator GO, 2026-07-02) — UI is FE-ONLY, no backend change
- **Fork 0 = ONE slice.** P9b = `/votes/:key` screen + "Call vote" configure dialog + MeetingWorkspace wiring + `api/votes.ts`, all on one branch `feat/P9b-voting-ui`.
- **6 design states are DERIVED** (not a toggle) from `vote.status` + whether current user (`auth.userId`) has a `Ballot` row + `HasCast`: Configured→not_open; Open+myBallot(!Recused)→open (change-until-close); Open+no-ballot→ineligible/view-only; Open+recused→recused; Closed|Ratified→closed.
- **Fork 1 (rec):** `double_error` contradicts shipped `ChangeBallot` — honor behavior SoT: ballot stays editable until close ("your recorded vote, change until close"), NOT a hard block. Update design.
- **Fork 2 (rec):** `quorum_failed` is NOT a persistent state — `close` returns 409 + vote stays Open; surface as error toast, drop the "Re-open/extend" CTA (no backend). Update design.
- **Fork 3 (confirmed):** render the closed/ratified chairman panel from `CounterName`/`ClosedAt`/`ResultSummary`; DEFER "View decision record" (no reverse vote→decision link in DTO) + "Record override" (no record/issue-from-vote UI exists) — omit, don't fake.
- **Fork 4 (follow design):** configure fixes `Options=["Approve","Reject"]` + `AllowAbstain`; tally maps Approve/Reject/Abstain → green/red/neutral.
- **Fork 5:** Call-vote dialog ALWAYS binds the current meeting (`MeetingId`) + topic; no standalone vote.
- **R-A reconciliation:** quorum pips = `Tally.CastCount` → `MinCast` (the DTO has no live present count; present-quorum is server-only at Open). Minor design reconcile.

## AC state after P9a
AC-021/022/023/024/025/026 Pending→**Partial** (domain+handler+HTTP; Met → P9b/P17 live real-stack per G-TRACE).
AC-015/016 (SoD-3 gate) + AC-052 (VoteOpened trigger) strengthened. Design SoT for P9b = the voting screen in
`ACMP Decision, Voting & ADR.dc.html` (guardrail #14).
