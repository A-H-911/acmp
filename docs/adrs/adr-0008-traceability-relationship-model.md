# ADR-0008: Typed Directed Relationship Edges as the Traceability Model

- Status: Accepted
- Date: 2026-06-24
- Deciders: Architecture Committee (secretary-confirmed)

## Context and Problem Statement

ACMP must support end-to-end traceability across its domain entities: Topics link to Meetings, Decisions, Actions, ADRs, Risks, Dependencies, Diagrams, Documents, Research Missions, and Architecture Invariants. Impact analysis ("what is affected if this system changes?") requires traversing these links. The design question is whether to use a shared identity scheme over a flat artifact table, a dedicated graph database, or explicit typed relationship edges in SQL.

## Decision Drivers

- The bounded-context structure (ADR-0001) means entities live in different modules. A shared identity layer is needed for cross-module relationships.
- A dedicated graph database (Neo4j, etc.) would be a second datastore — excluded by ADR-0003 (single SQL Server).
- SQL Server supports recursive CTEs and graph tables (`$node`, `$edge`) for traversal — adequate for the scale (≤20 users, bounded relationship set).
- Relationship types carry semantic meaning in governance (a Topic "resulted in" a Decision, a Decision "is implemented by" an Action, an ADR "supersedes" another ADR, a Dependency "blocks" a Topic). Untyped links lose this semantics.
- Traceability must be bidirectional (forward: what did this topic produce? backward: what topics led to this decision?) and impact analysis must work across module boundaries without modules reading each other's tables.

## Considered Options

1. **Typed directed `Relationship` edges (`{SourceArtifactId, TargetArtifactId, RelationshipType, CreatedBy, CreatedAt}`) over a shared `Artifact` identity registry** — explicit types, bidirectional traversal, SQL-native.
2. **Per-entity foreign key tables** (e.g., `TopicDecisions`, `TopicActions`, `DecisionADRs`) — expands combinatorially as entity count grows; each new entity type requires new join tables; no uniform traversal API.
3. **Neo4j or other graph DB** — second datastore (excluded by ADR-0003); operational complexity not justified at ≤20 users.
4. **Untyped association table** (`{SourceId, TargetId}`) — loses relationship type semantics; cannot express "resulted in" vs. "supersedes" vs. "blocks" in queries.

## Decision Outcome

Chosen option: "Typed directed `Relationship` edges over a shared `Artifact` identity table, traversed via recursive CTEs in SQL Server", because it preserves relationship semantics (governance requires knowing *how* entities are related, not just *that* they are), scales to the entity count without combinatorial table explosion, supports impact analysis via SQL traversal, and avoids a second datastore. The `Artifact` table acts as a polymorphic identity registry — each domain entity registers an artifact record on creation; the `Relationship` table links artifact IDs with a typed edge.

### Consequences

- Good: uniform traversal API across all entity types; relationship types are queryable and reportable; new entity types join the traceability graph by registering in `Artifact` (no schema change to `Relationship`); bidirectional traversal via `SourceArtifactId` / `TargetArtifactId` index; SQL Server graph tables or recursive CTEs support impact analysis without a graph DB.
- Bad / trade-off: polymorphic identity adds a level of indirection (every entity write must also write an `Artifact` row — enforced at the repository layer); cyclic relationship detection must be implemented at application level (SQL recursive CTEs can cycle); relationship type set must be curated and versioned (a proliferating type vocabulary becomes a governance problem in itself).

## Validation

- Unit tests: create a chain `Topic → Decision → Action → ADR`, traverse forward and backward, verify all nodes and edges are reachable.
- Impact analysis query: given a `System/Service` artifact, return all Topics that reference it, all Decisions that constrain it, and all Architecture Invariants that govern it — in a single SQL traversal.
- No cross-module direct table join: all cross-module relationships go via `Artifact` + `Relationship` (enforced in code review).

## Links / Notes

- Relationship types (initial set, not exhaustive): `ResultedIn`, `Supersedes`, `Blocks`, `DependsOn`, `ImplementedBy`, `EvidencedBy`, `AttachedTo`, `AssociatedWith`, `ConvertedTo`, `LinkedTo`. Full type vocabulary defined in `docs/domain/search-and-traceability.md`.
- Runtime entity ID prefixes that participate in the graph: `TOP-`, `MTG-`, `AGN-`, `MIN-`, `VOTE-`, `DECN-`, `ACT-`, `RSK-`, `DPN-`, `ADR-` (in-app), `AIV-`, `DOC-`, `DGM-`, `RMS-`, `FND-`, `REC-` — all register an `Artifact` row on creation.
- SQL Server graph table feature (`CREATE TABLE ... AS NODE / AS EDGE`) is available as an alternative to CTE-based traversal for deeply nested paths [unverified: confirm edition compatibility].
- Related: ADR-0001 (Search&Traceability as a cross-cutting module), ADR-0003 (single SQL Server for graph traversal), ADR-0009 (audit log entries also register as artifacts for traceability).
