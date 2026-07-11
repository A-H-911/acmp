# ADR-0026: Audit-Event Field Enrichment, Hash Versioning, and Same-Transaction Atomic Append (amends ADR-0009)

- Status: Proposed
- Date: 2026-07-11
- Deciders: Architecture Committee execution (secretary to ratify)
- Amends: ADR-0009 (Append-Only Audit Log with Immutability and Hash-Chain Integrity)

## Context and Problem Statement

ADR-0009 chose an append-only, hash-chained `AuditEvent` store and its *Validation* section already prescribed entries "with **actor, timestamp, entity reference, and change payload**" plus **same-transaction** append. The domain SSoT `docs/domain/audit-and-records.md §1.1/§1.2` specifies the full target schema (`ActorUserId, ActorRole, Action, SubjectType, SubjectId, Outcome, Before, After, CorrelationId, PrevHash, RowHash`) and requires the audit row be written **in the same DB transaction** as the state change (NFR-042).

The shipped implementation under-delivered against its own ADR: `AuditEvent` carries only `OccurredAt · EventType · Subject · DataJson · PrevHash · Hash` (verified: `src/BuildingBlocks/Acmp.Shared/Infrastructure/Audit/AuditEvent.cs`), and the append runs in a **separate `AuditDbContext` transaction after** the module save (verified: `CastBallot.cs:64-66`; no `TransactionScope`/`BeginTransaction`/`UseTransaction` anywhere in `src`). This blocks `AC-017` (which, with `FR-151`, demands entity-type/id, action-type, actor, before/after JSON, correlation id) and leaves a window where a committed state change can go unaudited if the second transaction fails — contrary to NFR-042.

Closing `AC-017/018/019/020` forces the decision now (the Audit slice). Enriching the row **changes the hash payload**, which touches **INV-005** ("hash-chain votes/decisions/audit") — hence this amending ADR rather than a silent build change.

## Decision Drivers

- `FR-151`/`AC-017` require the discrete field set; the lean row cannot satisfy them by a read view alone.
- Changing `ComputeHash` to cover new fields would make every **existing** row fail `Hash == Recompute()`, so the integrity check (`AC-019`) would report all pre-migration history as tampered — unacceptable for the record of record.
- NFR-042 (same-tx atomic append) is a documented requirement the current two-transaction model violates; the operator chose to close it in this slice.
- `docs/domain/audit-and-records.md §Privacy (C-PRIV-01)` requires storing **changed-field deltas, not full graphs or PII** — bounding what before/after capture.
- The platform already exposes the ingredients: `ModuleDbContext.SaveChangesAsync`/`StampAudit` loops `ChangeTracker.Entries<AuditableEntity>()` with `OriginalValues`/`CurrentValues` in hand (`ModuleDbContext.cs:21-44`); OpenTelemetry populates `Activity.Current.TraceId` per request; `ICurrentUser` exposes `Roles`.

## Considered Options

1. **Enrich the row + version the hash + capture before/after via a SaveChanges interceptor + one shared transaction** (this ADR). Faithful to `audit-and-records.md §1.1/§1.2`; preserves historical chain verifiability; before/after is automatic and privacy-bounded.
2. **Re-hash the whole chain under one new formula (no versioning).** Rejected — either rewrites immutable rows (defeats the point) or breaks verification of all history.
3. **Keep the lean row; satisfy AC-017 by reconciling the AC to the as-built model.** Rejected by the operator (chose literal compliance) and contradicts `FR-151`.
4. **Capture full aggregate graphs in before/after.** Rejected — over-builds and risks PII (C-PRIV-01); scalar changed-field deltas are what the doc wants.
5. **Keep the two-transaction append.** Rejected — violates NFR-042; a failed audit append after a committed change leaves an unaudited mutation.

## Decision Outcome

Chosen: **Option 1.**

- **Schema.** Add `ActorUserId` (the OIDC `sub` string, matching the existing `Subject` — not a `Guid`, avoiding a `User` lookup; a deviation from the doc's suggested `Guid?`, recorded here), `ActorRole` (from `ICurrentUser.Roles`), `Action`, `SubjectType`, `SubjectId`, `Outcome{Success,Denied,Failure}`, `BeforeJson`, `AfterJson`, `CorrelationId`, and `HashVersion`. `EventType`/`Subject` are retained for back-compat.
- **Hash versioning.** `ComputeHash`/`Recompute()` select the canonical payload by `HashVersion` (existing rows = 1, new rows = 2). The chain link (`PreviousHash` ← prior `Hash`) is version-agnostic, so v1→v2 rows link and verify continuously. The v2 canonical form is **deterministic** (round-trip `O` timestamps, ordinal field order, an explicit null token) — a golden-hash test pins it.
- **Before/after.** An `ISaveChangesInterceptor` on every `ModuleDbContext` captures each mutated `AuditableEntity`'s **changed scalar properties** (`OriginalValues`/`CurrentValues`) as `{SubjectType, SubjectId, Before, After}` into a request-scoped buffer; the enriched sink drains it by `(SubjectType, SubjectId)`. Denials/system events (no mutation) record `Outcome=Denied/Failure` with null before/after.
- **Same-transaction atomicity (NFR-042).** The module `DbContext` and `AuditDbContext` share one scoped `DbConnection`; a MediatR `TransactionBehavior` opens/commits a single transaction per command, so the state change and its audit append commit or roll back together. One shared connection (not `TransactionScope` across two connections) avoids MSDTC escalation.
- **Correlation** = `Activity.Current?.TraceId`.

### Consequences

- Good: `AC-017` becomes satisfiable end-to-end; the chain stays verifiable across the schema change; no committed state change can be unaudited (NFR-042); before/after is automatic and privacy-bounded.
- Trade-off: the `IAuditSink.EmitAsync` seam widens and the ~80 emit sites are reworked to pass `action/subjectType/subjectId/outcome` (staged behind a compatibility overload so `main` stays green; `AC-017` flips to Met only when all state-change sites are migrated). A single shared transaction holds the audit-tail lock slightly longer — negligible at ≤20 users; `sp_getapplock`/partitioning only if it ever binds.
- Out of scope (routed to P16, logged in the deferred-work register): DB-permission immutability (`INSERT/SELECT`-only on `audit.*`) and the scheduled nightly verification job (C-INS-02). `AC-018` is met by the application guard (no setters/delete path) + a test; `AC-019` by the on-demand verify endpoint.

## Validation

- Cross-version chain test: seed v1 rows, append v2 rows → `AuditChainVerifier.Verify` reports valid; tamper one row → the correct `BrokenAtSequence`.
- Golden canonical-hash test pinning the v2 payload serialization.
- Atomic-rollback integration test: force the audit append to throw → the state change is rolled back (no orphaned mutation, no orphaned audit row).
- Before/after test: a representative transition records scalar deltas only; a denial records `Outcome=Denied` with null before/after.

## Links / Notes

- Amends ADR-0009; realizes `docs/domain/audit-and-records.md §1.1/§1.2/§3`; satisfies `FR-150/151/152`, `AC-017/018/019`; honors INV-005, NFR-042, C-PRIV-01.
- Paired with ADR-0027 (audit-read RBAC). The `~80`-site count is treated as unverified until the migration PR re-greps it exhaustively.
