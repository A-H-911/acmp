# ADR-0030: Per-ballot crypto hash chaining for closed votes (D-13)

- Status: Accepted
- Date: 2026-07-16 (Proposed); ratified 2026-07-16
- Deciders: Architecture Committee execution; operator ratified 2026-07-16
- Context: P16 (Security hardening) — Batch 1, closing deferred-work item D-13

## Context and Problem Statement

Every vote **state change** (Configured/Open/Close/Ratify) is already hash-chained through the durable
`AuditEvent` store (BL-066, ADR-0009). But the individual ballot rows (`decisions.vote_ballots`) are **not**
themselves chained — the audit chain records *that* a vote closed, not the content of each ballot. A privileged
insider or DBA with direct SQL access (persona P3, threat T-04 / abuse-case AB-1) could therefore
`UPDATE decisions.vote_ballots SET choice = …` or delete a ballot on an already-closed vote and nothing would
detect it: app-layer immutability (no mutators + `RowVersion` + status guards) only defends writes that go
*through* the aggregate. This is the residual tracked as **D-13** (trigger: P16).

## Decision Drivers

- **Insider-first threat model** (`security-threat-model.md` P3, T-04, AB-1); tamper-evidence for A1 (votes).
- **C-IMM-04** (`security-controls.md`): tamper-evidence hash chain on the most critical records.
- **Proportional to ≤20 users on-prem** — no PKI/HSM (RISK-SEC-002); tamper-*evidence*, not tamper-proofing.
- **Reuse, don't reinvent** — mirror the proven `AuditEvent` SHA-256 chain (`AuditChainVerifier`).
- **Module boundary** (ADR-0001) — the Decisions module verifies its own aggregate; no cross-module reads.

## Considered Options

1. **Do nothing** — rely on app-layer immutability + `RowVersion`. Rejected: leaves the direct-SQL/DBA gap open
   (the exact P3 threat the governance system exists to resist).
2. **Hash-chain the ballot rows, sealed at Close** (chosen). A SHA-256 chain over all ballot rows makes a
   content edit, insert, delete, or reorder detectable.
3. **Per-ballot digital signatures** (asymmetric). Rejected: needs key management/PKI — disproportionate at this
   scale ([L3-skip], RISK-SEC-002); adds no detection the hash chain lacks for an on-prem single-writer system.

## Decision Outcome

Chosen: **option 2.** At `Vote.Close` (where the tally already freezes), seal a hash chain over **all** ballot
rows:

- Order ballots deterministically by voter `sub` (stable, infra-independent — the verifier reproduces the order
  without trusting EF's load order); the hash includes the row's **position index**, so insert/delete/reorder is
  detectable, not only content edits.
- Each ballot `Hash = SHA-256(bc1 | index | voterSub | choice | recused | castAt | commentEn | commentAr | prevHash)`,
  `PreviousHash` linking to the prior ballot, genesis (`0…0`) for the first — the same canonical, null-flag-prefixed
  formula shape as `AuditEvent.ComputeHashV2`. The hashing lives self-contained in `Decisions.Domain`
  (`BallotChain`); it is **not** extracted to a shared helper unless it becomes byte-identical to the audit one
  (avoids coupling Shared↔Decisions).
- **Legacy handling (no backfill):** votes closed before P16 have `ChainSealedAt = null` and are reported
  `Unsealed` (not a tamper) — the verifier skips them. Backfilling historical ballot hashes was rejected as
  disproportionate; new closes are chained, and the AuditEvent state-change chain already covers pre-P16 closes.
- **Verification:** `Vote.VerifyBallotChain()` (first broken index + reason) and `Vote.VerifyTally()` (the frozen
  `tally_json` must still equal a recompute from the chain-verified ballots — catches a forged tally). Both are
  driven nightly by the `IIntegrityVerifier` (D-16/C-INS-02) via a per-module `IIntegrityCheck` seam
  (`VoteChainIntegrityCheck`), which rides ADR-0001 (a read-only same-module check, no new write seam).

## Consequences

- **Positive:** a direct-SQL edit/delete/reorder of a closed vote's ballots, or a forged tally, becomes
  detectable and is alerted + audited nightly. Closes D-13. Two nullable columns (`vote_ballots.PreviousHash/Hash`)
  + `votes.ChainSealedAt`; no new dependency; no PKI.
- **Negative / accepted:** legacy pre-P16 closed votes are not ballot-chained (accepted — proportional).
  Tamper-*evidence*, not prevention: **RISK-SEC-001** stands — colluding chairman+secretary can still co-attest a
  fraudulent vote; the chain makes it detectable and non-repudiable, not impossible. The `bc1` payload formula is
  now a compatibility contract (changing it invalidates every sealed ballot), exactly like the audit formula.

## Traceability

Implements D-13. Threats T-04/T-21, AB-1 (`security-threat-model.md`); control C-IMM-04 (`security-controls.md`,
ASVS V16/V11). Reuses ADR-0009 (`AuditEvent` chain) / ADR-0026; respects ADR-0001 (module boundary). Verified by
`VoteBallotChainTests`, `VoteChainIntegrityCheckTests`, `IntegrityVerifierTests` (`Category=Security`).
