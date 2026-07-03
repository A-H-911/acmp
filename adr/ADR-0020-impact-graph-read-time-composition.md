# ADR-0020: Impact graph by read-time contract composition, not a cross-schema traversal (clarifies ADR-0019)

- Status: Accepted
- Date: 2026-07-03
- Deciders: Architecture Committee execution (secretary to ratify)
- Clarifies: ADR-0019 (self-describing edges) — its Decision Outcome named "SQL-native traversal (recursive CTE, P10f)"

## Context and Problem Statement

P10f builds the transitive impact graph (FR-096): the depth-bounded subgraph around a focus artifact, spanning **both** governed edge stores — the Traceability module's typed `Relationship` edges **and** the Dependencies module's `Dependency` edges. Read-time composition is locked (OQ-046 / ASM-016): a dependency edge is stored once and is **not** mirrored into the Relationship table, so any traversal that walks only one store misses the other. ADR-0019 offhandedly named a "recursive CTE" for this traversal, but a recursive CTE spanning the `traceability` and `dependencies` schemas is a **cross-module table join** — a direct ADR-0001 violation (a module reading another module's tables). The operator also asked (against the reviewers' lean) that the traversal run **server-side** and that FR-095 cross-stream be built now. This ADR records how that is done without breaking module isolation.

## Decision Drivers

- ADR-0001: a module never reads another module's tables — cross-module reads go through `Acmp.Shared` contracts / MediatR only. A two-schema recursive CTE breaks this.
- Read-time composition is settled (OQ-046, ASM-016): union the two stores at read time; never write-through-mirror.
- Right-sizing (#12): ≤20 users, depth ≤3, tiny governance graphs — the traversal cost is trivial; the constraint is isolation, not throughput.
- The two per-artifact reads already exist (`GetArtifactRelationships`, `GetDependenciesForArtifact`) and each self-composes **within** its own module; the graph only needs to orchestrate them.

## Decision Outcome

Chosen: **the impact graph is composed at read time by a breadth-first walk in the Traceability module (`GetImpactGraph`), orchestrating each module's existing per-artifact read through `Acmp.Shared` ports — no cross-schema query.**

- The BFS lives in `Traceability.Application` (the Search & Traceability home, docs/30). Per node it unions its own `Relationship` edges with the Dependencies module's edges read through a **new shared port `IDependencyArtifactReader`** (implemented in `Dependencies.Infrastructure` over the same `GetDependenciesForArtifact` read — one source of truth). The port's DTO lives in `Acmp.Shared` and speaks **primitives** (string type/kind/status names); the Dependencies enums never leak into the kernel (mirrors `ITraceabilityLinks`).
- FR-095 cross-stream is **Topic-scope** (Option A): a second shared port `ITopicStreamReader` (implemented in `Topics.Infrastructure`) returns a topic's affected-stream **codes**; an edge is cross-stream when both ends are Topics with disjoint non-empty code sets. Non-Topic endpoints carry no stream, so their edges are never cross-stream. The inherit-from-topic model is deferred (**OQ-047**).
- Traversal guards: visited-set keyed on the **type-name string + id** (not the enum ordinal — `ArtifactType.Topic` and `DependencyEndpointType.Topic` are different ints; only the names union a node reached via both stores), a `MaxNodes` ceiling, depth clamped 1–3, and per-node try/catch so one failed read degrades that node to a leaf and flags `partial` rather than blanking the graph.
- Endpoint: `GET /api/traceability/graph/{type}/{id}?depth=1..3` → `{ focus, nodes, edges, partial }`; read-all (any authenticated committee member).

This **clarifies** ADR-0019: replace its "recursive CTE" phrasing with "read-time contract composition." ADR-0019 stands in every other respect (self-describing edges, no Artifact registry).

### Consequences

- Good: zero cross-module table reads; the two new ports are the sanctioned ADR-0001 shape already proven by `ITraceabilityLinks` / `IActionLinkDirectory`; no new tables or migration (pure read composition); new artifact types join the graph with no schema change.
- Trade-off: the walk issues N per-node reads instead of one query. Trivial at this scale (≤20 users, depth ≤3, `MaxNodes` cap); if a graph ever measurably drags, tier-batched reads are the upgrade path (`ponytail:` noted in the composer).
- Trade-off: **FR-095 is Topic-scope only** — most governance edges are cross-TYPE (Topic→Decision→Action), so the cross-stream signal lights up rarely. Recorded honestly as FR-095 **partial**, not "built" (OQ-047 tracks the richer inherit model).
- Trade-off: far-node **lifecycle status** is not shown (a cross-module read); the node carries only type/key/title/tier/blocked/streams. `Blocked` is derived from incident blocker dependency edges (`IsBlocker`) — an honest approximation of "is blocked."

## Validation

- `ImpactGraphTests` (15 cases) drive the composer through its handler with faked ports: signed tiers, depth bound + clamp, cycle termination, edge dedup, dependency union + blocked, inbound-upstream, System dead-end + unmapped-type skip, partial-on-failure, Topic-scope cross-stream (cross / shared / empty / non-Topic), stream-read-failure, and the node ceiling.
- `TraceabilityApiTests` walk a real Topic→Decision→Action chain end-to-end through the wired ports (depth 1 vs 2) + 401 without a token.
- ArchUnit `Traceability_should_not_depend_on_other_modules` stays green — the graph consumes `Acmp.Shared` ports only, never `Dependencies.*` / `Topics.*`.

## Links / Notes

- Clarifies ADR-0019; builds on ADR-0001 (module isolation), OQ-046 / ASM-016 (read-time composition, no mirror).
- New ports: `Acmp.Shared/Contracts/Dependencies/IDependencyArtifactReader.cs`, `Acmp.Shared/Contracts/Topics/ITopicStreamReader.cs`.
- Open: **OQ-047** (stream-inheritance model for whole-graph cross-stream; default = this Topic-scope).
- Canonical endpoint note: `docs/30-search-and-traceability.md`. Recorded in the P10f progress-log entry + acceptance-audit (FR-096 Met, FR-095 partial).
