# ADR-0021: Cross-module system writes go through a Shared.Contracts write port (extends ADR-0001)

- Status: Accepted
- Date: 2026-07-04
- Deciders: Architecture Committee execution (secretary to ratify)
- Extends: ADR-0001 (modular monolith, module isolation)

## Context and Problem Statement

FR-068 (Decision→ADR promotion, P11e) requires the Governance module, when it creates an ADR from a decision, to also record a **bidirectional link** between the two artifacts. The idiomatic ACMP link is a typed `Relationship` edge (ADR-0008/0019) rendered in both artifacts' traceability panels — but that edge lives in the **Traceability** module's store. Governance therefore needs to *write* an edge it does not own.

Every existing cross-module seam in `Acmp.Shared.Contracts` is **read-only** (`ITraceabilityLinks`, `IActionLinkDirectory`, `IMeetingQuorumSource`, `IDependencyArtifactReader`, and the new `IDecisionReader`). There is no established pattern for a module-initiated cross-module **write**, and no precedent for one module sending another module's MediatR command (which would couple `Governance.Application` to `Traceability.Application`). CLAUDE.md's module rule permits communication "via in-process public contracts / MediatR / domain events only" — this ADR fixes which of those to use so the precedent is deliberate, not incidental (guardrail #11).

## Decision Drivers

- ADR-0001 forbids a module reading or writing another module's tables directly.
- Referencing `Traceability.Application`'s `CreateRelationshipCommand` from `Governance.Application` would be a new module→module application-layer dependency (ArchUnit-visible) and carries the command's user-facing RBAC (`Traceability.Link` = Chairman/Secretary), which is the wrong authorization for a *system consequence* of an already-authorized action.
- The read-seam pattern (a primitive-only port in `Acmp.Shared.Contracts`, implemented in the owning module's Infrastructure) is already proven five times and keeps the shared kernel free of the owning module's enums.
- A domain-event → subscriber approach is more decoupled but heavier machinery than a ≤20-user on-prem tool needs for a synchronous, in-transaction consequence (guardrail #12).

## Decision Outcome

Chosen: **a Shared.Contracts write port, symmetric with the read seams.** `ITraceabilityWriter` exposes `RecordEdgeAsync(...)` in primitives (artifact/relationship types as string names); the Traceability module implements it in its Infrastructure over the same `Relationship` store the UI uses. Consuming modules depend only on the shared port, never on `Traceability.Application`. The write is:

- **Unauthorized at the port** — it carries no RBAC of its own; the *calling* action must already be authorized (FR-068 promotion is `Adr.Promote` = Chairman-only). The port is a system consequence, not a user action.
- **Idempotent** per `(source, target, relType)` so a retried caller never duplicates the edge.
- **Audited** (`Relationship.Created`, with a `System=true` marker distinguishing it from a user-created edge).

This is the sanctioned pattern for any future module-initiated cross-module write: add a primitive write port to `Acmp.Shared.Contracts`, implement it in the owning module's Infrastructure. Direct cross-module command sends remain disallowed.

### Consequences

- Good: no `Governance.Application → Traceability.Application` coupling; the shared kernel stays enum-free; the write reuses the owning module's store as the single source of truth; the pattern generalizes.
- Trade-off: a system write bypasses the user-facing `CreateRelationship` command's validation/authorization. Accepted — the port applies the same domain guards (`Relationship.Create`) and the caller is separately authorized; the audit `System=true` marker keeps the trail honest.
- Trade-off: two write paths now exist for edges (the user command and the system port). Accepted — they share the domain factory and store; only the entry authorization differs.

## Validation

- P11e unit tests exercise the port directly: a system edge is persisted + audited, a repeat is a no-op (idempotent), and an unknown type name is rejected.
- The FR-068 happy path (Chairman promotes an issued decision → a `RecordedAs` Decision→ADR edge is written) is covered end to end by the endpoint contract test.
- ArchUnit continues to enforce that only the Traceability module writes the `traceability` schema (the port's impl lives there).

## Links / Notes

- Extends ADR-0001 (module isolation); related ADR-0008/0019 (relationship edges), ADR-0009 (relationship changes are audit events).
- Consumed first by P11e (`PromoteDecisionToAdr`); recorded in the P11e progress-log entry.
