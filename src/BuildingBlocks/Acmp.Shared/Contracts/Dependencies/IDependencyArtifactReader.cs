namespace Acmp.Shared.Contracts.Dependencies;

// Cross-module read seam (ADR-0001, P10f): the Traceability impact-graph composer reads one artifact's
// governed dependency edges without ever touching the Dependencies module's tables. Implemented in
// Dependencies.Infrastructure over the SAME read the /api/dependencies/artifact panel uses (one source of
// truth). Speaks only primitives — the Dependencies enums (endpoint type / kind / status) never leak into
// the shared kernel; they travel as their string names, exactly as the wire contract already projects them
// (mirrors ITraceabilityLinks). IsBlocker stays server-DERIVED (Kind ∈ {BlockedBy, Blocks} && Status==Open).

// One dependency edge relative to the queried artifact (the "other" end is the far endpoint). Direction is
// implied by the list it sits in: Outbound = the queried artifact is the From end; Inbound = it is the To end.
public sealed record DependencyGraphEdge(
    Guid Id,
    string Key,
    string OtherType,
    Guid OtherId,
    string OtherKey,
    string OtherTitle,
    string Kind,
    string Status,
    bool IsBlocker);

// The queried artifact's dependency edges (Removed edges excluded, as the panel read excludes them).
public sealed record DependencyGraphEdges(
    IReadOnlyList<DependencyGraphEdge> Outbound,
    IReadOnlyList<DependencyGraphEdge> Inbound);

public interface IDependencyArtifactReader
{
    // typeName = a DependencyEndpointType name (Topic/Action/System/Decision). An unknown/unmapped name
    // returns empty edges (never throws) — the graph composer already guards, this is defence in depth.
    Task<DependencyGraphEdges> GetForArtifactAsync(string typeName, Guid id, CancellationToken ct = default);
}
