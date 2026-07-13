# ADR-0028: Serialize Audited Write-Commands via a Transaction-Scoped Application Lock (amends ADR-0026)

- Status: Accepted
- Date: 2026-07-13
- Deciders: Architecture Committee execution (secretary-ratified 2026-07-13)
- Amends: ADR-0026 (Audit-Event Field Enrichment, Hash Versioning, and Same-Transaction Atomic Append)

## Context and Problem Statement

ADR-0009/0026 made the `AuditEvent` store an append-only, hash-chained log with **UNIQUE indexes on both
`PreviousHash` and `Hash`** (`IX_AuditEvents_PreviousHash`, `IX_AuditEvents_Hash`) so the chain cannot fork, and
made the audit append **same-transaction** with the state change (NFR-042). Each append reads the current chain
tip (`SqlAuditSink.TipHashAsync`) and inserts a row whose `PreviousHash` = that tip.

The `SqlAuditSink` `ponytail:` note accepted, without an application lock, that two **concurrent** appends both
read the same tip and only one can insert — the loser hitting a duplicate-key (SQL 2601/2627) — as negligible at
this deployment's scale (on-prem, ≤20 users). **D-18 disproved "negligible in practice":** the P15b live VR
reproduced it on the very first run (the SPA's login-time `Membership.ProfileSynced` append raced a seed
`Research.MissionProposed`, both chained off tip `1f738021` → the loser 500'd and, being same-transaction, rolled
its whole command back). A subsequent 8-way integration stress test then surfaced a **worse** failure mode in CI:
a **deadlock (SQL 1205)**, whose victim's transaction is rolled back entirely — so an in-place retry cannot even
recover it.

The committee chose to close D-18 with a robust, any-concurrency fix rather than accept the tradeoff to P16.

## Decision Drivers

- The failure is on a governance **invariant** path (INV-005 hash-chain; NFR-042 same-tx atomicity) — a lost or
  unaudited state change is unacceptable for the system of record.
- A retry alone is insufficient: a **1205 deadlock** dooms the transaction, so re-reading the tip and re-inserting
  in place is impossible.
- A lock taken at the **audit append** (inside `SqlAuditSink`) is **lock-order-unsafe**: handlers are not uniform —
  most write then emit, but multi-write handlers emit then write more (`VerifyAction` emit→notify; `IssueDecision`
  emit→`DecisionIssuance.ApplyAsync` writes topics + traceability; `ConductMeeting` emit mid-handler). A
  transaction-held lock acquired at emit would order `{lock, module-rows}` oppositely across handlers → a new
  cross-handler deadlock.
- `WITH (UPDLOCK, HOLDLOCK)` on the tip-read was rejected: serializable range locks on inserts into the same key
  range are the classic serializable-insert deadlock — strictly worse than one named mutex.
- Scale (≤20 users, low traffic, on-prem) makes **serializing audited write-commands** an acceptable throughput
  cost.

## Considered Options

1. **Bounded optimistic retry in `SqlAuditSink` only.** Fixes the real 2-way race (block-then-2601 → re-read →
   converge) but cannot recover a 1205 deadlock, which the 8-way case produces. Kept as the **denial/autocommit
   backstop** (see Decision), not as the sole mechanism.
2. **App lock at the audit append (in the sink), held to commit.** Rejected — lock-order-unsafe (above).
3. **`sp_getapplock` at tx-open, before any module write (this ADR).** Every write-command acquires the audit-chain
   lock first, then module rows — one consistent order, no cross-handler cycle — and audit appends serialize as a
   consequence. Chosen.
4. **`UPDLOCK, HOLDLOCK` tip-read.** Rejected — serializable-insert deadlock.
5. **Per-stream chain partitioning / an append queue / async out-of-band audit.** Rejected — over-built for the
   scale; one global chain, serialized, is enough (YAGNI).

## Decision

Acquire a **transaction-scoped exclusive application lock** (`sp_getapplock @Resource='acmp-audit-chain',
@LockMode='Exclusive', @LockOwner='Transaction'`) in `AmbientTransaction.EnsureStartedAsync` — i.e. **at tx-open,
immediately after `BeginTransaction`, before the first module write**. Because the ambient transaction opens
lazily on the first module write, this serializes **every audited write-command**: a command holds the lock from
tx-open until `TransactionBehavior` commits/rolls back, so the next command reads a **committed** tip. This makes
the write-command path both fork-free (no 2601) and deadlock-free (consistent lock order + no crosswise tip
contention). It is SQL-Server-only (`sp_getapplock`); a non-SqlServer connection (only hypothetical, in tests)
degrades to no serialization rather than erroring. A lock-timeout raises (`THROW`) so the command fails closed
rather than proceeding unaudited.

**Denial / autocommit path.** A denial (e.g. SoD/`already-voted`) writes no module entity, opens no ambient
transaction, and so holds no tx-open lock; it autocommits its `Denied` row. Concurrent denials can still fork the
chain but — having no crosswise module locks — can only fork, never deadlock. The **bounded retry** in
`SqlAuditSink.AppendAsync` (re-read the advanced tip, recompute, re-insert; ≤16 attempts; catches a 2601/2627 on
either the `PreviousHash` **or** `Hash` index; fails closed on exhaustion) remains as the backstop for this path.

## Consequences

- **Positive:** the audit hash-chain is provably non-forking and deadlock-free under any concurrency; INV-005 and
  NFR-042 hold under contention; no lost or unaudited state change.
- **Negative / accepted:** audited write-commands **serialize globally** (one committing at a time). At ≤20 users
  low-traffic this is negligible; if throughput ever matters, partition the lock per stream — not before.
- **Scope:** no schema/migration change (the lock is runtime-only). `INV-005` is *strengthened*, not changed, so
  this amends ADR-0026 rather than superseding it.
- **Verification:** two Testcontainers integration tests — 8 concurrent write-commands converge with the chain
  intact (serialization), and 8 concurrent denials converge via the retry (backstop) — plus the existing
  same-transaction atomicity suite, all green; the write-command case is proven green **in CI** (where the 1205
  first surfaced).
