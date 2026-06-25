# ADR-0009: Append-Only Audit Log with Immutability and Hash-Chain Integrity

- Status: Accepted
- Date: 2026-06-24
- Deciders: Architecture Committee (secretary-confirmed)

## Context and Problem Statement

ACMP is the system of record for architectural governance: votes, decisions, ADRs, and published minutes have legal and regulatory weight. A governance platform that allows silent editing of these records is unfit for purpose. An audit strategy must be chosen that supports accountability, provides tamper evidence, and satisfies records-management requirements — while remaining operable without cryptographic PKI infrastructure on an on-prem VM.

## Decision Drivers

- Votes and issued decisions are governance records that must be immutable (editing a vote outcome after the fact would constitute fraud; similarly for committee decisions and published minutes).
- An append-only audit log covering all create/update/delete/access events provides a reconstruction timeline for any dispute or compliance review.
- A hash-chain (each audit entry includes a hash of the previous entry's hash + current content) provides tamper evidence without requiring an external timestamping authority — implementable entirely within SQL Server.
- "Keep everything" retention policy (no auto-purge in v1; configurable for future legal retention periods) aligns with immutable records as the default state.
- The in-app ADR lifecycle mirrors this: `Draft → Proposed → Approved → (Superseded | Deprecated)`; a superseded ADR is never deleted or edited — the superseding ADR references it.
- Published minutes follow the same pattern: a correction produces a new version; the original is retained and marked superseded.

## Considered Options

1. **Append-only audit table + immutable enforcement on votes/decisions/ADRs/minutes + hash-chain for high-sensitivity records** — tamper-evident, in-SQL, no external dependency.
2. **Audit log only (no hash-chain)** — simpler; audit records could be modified by a DBA with direct DB access; provides reconstruction timeline but no tamper evidence.
3. **External audit service / blockchain anchoring** — adds an external runtime dependency (violates CON-001 spirit); unjustified at ≤20 users; operationally complex.
4. **Soft-delete with update history** — does not prevent record modification; does not meet the "votes are immutable" requirement.

## Decision Outcome

Chosen option: "Append-only audit log + immutability enforcement at the application layer + hash-chain on the highest-sensitivity record categories (votes, decisions, audit log itself)", because it provides tamper evidence within the existing SQL Server infrastructure without external dependencies, and the immutability rule is enforced both at the application layer (no UPDATE/DELETE paths on immutable entities) and by DB constraints (no `UPDATE` privilege on the audit table for the app DB user).

### Consequences

- Good: any post-hoc modification of a vote or decision is detectable via hash-chain verification; the audit log provides a complete reconstruction timeline; immutability is enforced at multiple layers (application + DB permissions); no external dependency; hash-chain verification can be run as a scheduled integrity check.
- Bad / trade-off: immutable records require a "supersede" workflow rather than "edit" for corrections (e.g., correcting a vote that was cast in error requires a chairman-authorized override event, not an edit) — this adds UX complexity but is the correct governance behaviour. The hash-chain covers tamper detection by insiders with application access; a DBA with direct SQL access and truncate rights could still destroy the chain — mitigate by restricting DB permissions and enabling SQL Server audit at the server level.

## Validation

- Unit test: attempt to UPDATE a closed `Vote` record via the application — expect a `DomainException` (operation not permitted).
- Integration test: insert 10 audit events; tamper with event 5; run hash-chain verification — expect a verification failure reported at event 6.
- Audit coverage test: perform a representative set of create/update/publish/vote/decide operations; verify each produces a corresponding audit entry with actor, timestamp, entity reference, and change payload.
- DB permission test: confirm the ACMP app DB user has no `UPDATE` or `DELETE` privilege on the `AuditLog` table.

## Links / Notes

- Immutable entity categories: `Vote` (status `Closed` → `Ratified`), `CommitteeDecision` (issued), `PublishedMinutes`, `ApprovedADR` (in-app), `AuditLog` entries.
- Correction workflow: chairman or secretary raises a superseding event (e.g., `VoteOverrideEvent`, `DecisionAmendmentEvent`) recorded as a new immutable event; the original is preserved and marked as superseded by the new event ID.
- Hash-chain implementation: `AuditEntry.Hash = SHA-256(PreviousEntryHash || EntryContent)`. The genesis entry uses a fixed known nonce as the previous hash.
- Retention: all audit records kept indefinitely in v1; configurable retention thresholds available for future legal requirements.
- Scheduled integrity check: a Hangfire recurring job runs hash-chain verification nightly and writes the result to a health endpoint; failures alert via in-app notification.
- Related: ADR-0003 (SQL Server hosts audit table), ADR-0010 (vote attribution — every voter identity is recorded in the immutable vote record), ADR-0014 (Hangfire runs integrity check job).
