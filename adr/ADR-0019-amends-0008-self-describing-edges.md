# ADR-0019: Self-describing relationship edges, no physical Artifact registry (amends ADR-0008)

- Status: Accepted
- Date: 2026-07-03
- Deciders: Architecture Committee execution (secretary to ratify)
- Amends: ADR-0008 (Typed directed relationship edges as the traceability model)

## Context and Problem Statement

ADR-0008 chose typed directed `Relationship` edges over SQL for traceability, and its *Decision Outcome* section additionally prescribed a **polymorphic `Artifact` identity registry**: "each domain entity registers an artifact record on creation… every entity write must also write an `Artifact` row (enforced at the repository layer)." However, the canonical schema in `docs/30-search-and-traceability.md §2.1` has **no** such table — its `Relationship` row stores `(SourceType, SourceId)` / `(TargetType, TargetId)` directly, with no FK to a registry. Code and a settled ADR disagree; CLAUDE.md requires we resolve it in an ADR rather than let it drift (guardrail #11). P10c (the Traceability backend) is the first slice to build edges, so it forces the decision.

## Decision Drivers

- A physical `Artifact` registry would require **every module** to write a row into the traceability schema on **every aggregate create** — a direct ADR-0001 module-isolation violation (a module writing another module's tables), or an eventing/repository coupling that is heavy for an on-prem ≤20-user tool (guardrail #12).
- The codebase already proved the FK-less soft-reference pattern **twice**: the Risks module stores `(SubjectType, SubjectId, SubjectKey)` value snapshots with no cross-module navigation, and `IActionLinkDirectory` answers a cross-module "does a link exist?" in primitives.
- `docs/30 §2.1` — the authoritative SQL — already models edges as self-describing `(Type, Id)` pairs; matching it removes the drift with the least code.
- Deep-link navigation needs only the target's **display key** (stable, e.g. `TOP-2026-042`); the AC-062 panel additionally displays a **title**, which can be captured as a create-time snapshot (the same accepted-staleness pattern used for owner-name snapshots everywhere in ACMP).

## Decision Outcome

Chosen: **self-describing edges, no physical `Artifact` registry.** A `Relationship` row carries both endpoints as value snapshots — `(SourceType, SourceId, SourceKey, SourceTitle)` and `(TargetType, TargetId, TargetKey, TargetTitle)` — plus `RelType`, optional `Notes`, `IsActive` soft-delete, and audit stamps. No entity writes an `Artifact` row on create. ADR-0008 remains in force in every other respect: typed directed edges, SQL-native traversal (recursive CTE, P10f), bidirectional navigation, audited relationship changes (ADR-0009), and the `RelationshipType` catalog in `docs/30 §2.2`.

### Consequences

- Good: no cross-module write coupling; matches `docs/30 §2.1` exactly; reuses the proven Risks soft-ref pattern; new artifact types join the graph with zero schema change (they are just new `ArtifactType` enum values on the edge).
- Trade-off: the `SourceTitle`/`TargetTitle` snapshots can go **stale** if the referenced artifact is later renamed (accepted — deep-linking fetches live data; same trade as owner-name snapshots). The display **key** never goes stale (keys are immutable). Live status is **not** snapshotted (it churns) — status is out of scope for the AC-062 panel and resolved later if needed.
- Trade-off: there is no central registry to enumerate "all artifacts"; that was never required (search is FTS per ADR-0011, not a registry scan).

## Validation

- P10c unit tests create an edge, read it back through the panel query in both directions, and assert the snapshots (key + title) round-trip.
- No module writes the `traceability` schema except the Traceability module (ArchUnit `Traceability_should_not_depend_on_other_modules` + the module-isolation facts).

## Links / Notes

- Amends ADR-0008 (`adr/ADR-0008-traceability-relationship-model.md`).
- Canonical schema: `docs/30-search-and-traceability.md §2.1`; type catalog §2.2.
- Recorded in the P10c progress-log entry and acceptance-audit (ASM-P10c-1).
- Related: ADR-0001 (module isolation), ADR-0003 (single SQL Server), ADR-0009 (relationship changes are audit events).
