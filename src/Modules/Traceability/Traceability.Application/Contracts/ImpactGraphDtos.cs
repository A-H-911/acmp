namespace Acmp.Modules.Traceability.Application.Contracts;

// Read model for the P10f impact graph (FR-096): the depth-bounded subgraph around a focus artifact, composed
// at read time from this module's typed Relationship edges UNIONed with the Dependencies module's governed
// edges (read through the Acmp.Shared IDependencyArtifactReader port — never its tables, ADR-0001). Enums
// project as string names (RelationshipType / DependencyKind), localized by the SPA. Node streams are the
// Topic-scope FR-095 signal (codes; only Topic nodes carry them). The SPA lays out columns from the signed
// Tier and colours edges from Rel + Source; the backend owns only the traversal, union, and cross-stream math.

// One node. Key/Title are the create-time snapshots carried on the discovering edge (ADR-0019); the FOCUS node
// has empty Key/Title (its own identity is not on any edge — the SPA already holds it). Tier is the signed BFS
// distance: 0 = focus, negative = upstream (reached via an incoming edge), positive = downstream. Blocked =
// the node touches an active blocker dependency edge (IsBlocker) — an honest approximation of "is blocked".
public sealed record ImpactGraphNodeDto(
    string Type,
    Guid Id,
    string Key,
    string Title,
    int Tier,
    bool Blocked,
    IReadOnlyList<string> Streams);

// One directed edge between two nodes. Source = "rel" (typed Relationship) or "dep" (governed Dependency);
// Rel is the RelationshipType or DependencyKind name. IsCrossStream is the FR-095 Topic-scope signal (both
// ends are Topics with disjoint non-empty stream sets); it is always false for any edge touching a non-Topic.
public sealed record ImpactGraphEdgeDto(
    string Source,
    string Rel,
    string FromType,
    Guid FromId,
    string ToType,
    Guid ToId,
    bool IsBlocker,
    bool IsCrossStream);

// The composed subgraph. Partial = the traversal hit the node ceiling or a node read failed (the SPA shows a
// soft "partial graph" notice rather than blanking) — never a silent truncation.
public sealed record ImpactGraphDto(
    string FocusType,
    Guid FocusId,
    int Depth,
    IReadOnlyList<ImpactGraphNodeDto> Nodes,
    IReadOnlyList<ImpactGraphEdgeDto> Edges,
    bool Partial);
