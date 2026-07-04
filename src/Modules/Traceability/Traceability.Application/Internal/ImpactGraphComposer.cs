using Acmp.Modules.Traceability.Application.Contracts;
using Acmp.Modules.Traceability.Domain.Enums;
using Acmp.Shared.Contracts.Dependencies;

namespace Acmp.Modules.Traceability.Application.Internal;

// The pure P10f traversal (FR-096). A breadth-first walk from the focus artifact, unioning typed Relationship
// edges with governed Dependency edges per node, bounded by depth and a node ceiling. Fetching is injected as
// three delegates so the whole thing unit-tests deterministically with no DB or HTTP (the handler wires the
// delegates to the real reads). Faithful to the reference `buildTiers`: a node's Tier is the SIGNED BFS level
// at first discovery (+level via an outgoing/outbound edge = downstream, −level via incoming/inbound = up),
// set once and never overwritten. Cross-stream + blocked are computed in post-passes once all nodes are known.
internal static class ImpactGraphComposer
{
    public const int MaxNodes = 60;

    // The four DependencyEndpointType names (Topic/Action/System/Decision). Only these carry dependency edges;
    // "System" is dependency-only (not an ArtifactType), so it never gets a relationship read — it just
    // dead-ends when it has no further dependency edges.
    private static readonly HashSet<string> DepEndpointNames = new(StringComparer.Ordinal)
        { "Topic", "Action", "System", "Decision" };

    public delegate Task<ArtifactRelationshipsDto> FetchRelationships(ArtifactType type, Guid id, CancellationToken ct);
    public delegate Task<DependencyGraphEdges> FetchDependencies(string typeName, Guid id, CancellationToken ct);
    public delegate Task<IReadOnlyList<string>> FetchStreams(Guid topicId, CancellationToken ct);

    private sealed class NodeAcc
    {
        public required string Type;
        public required Guid Id;
        public string Key = "";
        public string Title = "";
        public int Tier;
        public string[] Streams = Array.Empty<string>();
        public bool Blocked;
    }

    private sealed class EdgeAcc
    {
        public required string Source;
        public required string Rel;
        public required string FromType;
        public required Guid FromId;
        public required string ToType;
        public required Guid ToId;
        public bool IsBlocker;
        public bool IsCrossStream;
    }

    public static async Task<ImpactGraphDto> BuildAsync(
        ArtifactType focusType, Guid focusId, int depth,
        FetchRelationships fetchRelationships, FetchDependencies fetchDependencies, FetchStreams fetchStreams,
        CancellationToken ct)
    {
        depth = Math.Clamp(depth, 1, 3);

        var nodes = new Dictionary<string, NodeAcc>(StringComparer.Ordinal);
        var edges = new Dictionary<string, EdgeAcc>(StringComparer.Ordinal);
        var expanded = new HashSet<string>(StringComparer.Ordinal);
        var partial = false;

        string KeyOf(string type, Guid id) => $"{type}:{id}";

        // First discovery wins the tier (mirrors the reference `!(x in tier)` guard); a later/deeper sighting
        // never moves a node. Returns the node key when it is NEW (so the caller queues it), else null.
        string? AddNode(string type, Guid id, string key, string title, int tier)
        {
            var k = KeyOf(type, id);
            if (nodes.TryGetValue(k, out var existing))
            {
                if (existing.Key.Length == 0 && key.Length > 0) { existing.Key = key; existing.Title = title; }
                return null;
            }
            if (nodes.Count >= MaxNodes) { partial = true; return null; }
            nodes[k] = new NodeAcc { Type = type, Id = id, Key = key, Title = title, Tier = tier };
            return k;
        }

        void AddEdge(string source, Guid edgeId, string rel, string fromType, Guid fromId,
            string toType, Guid toId, bool isBlocker)
        {
            var k = $"{source}:{edgeId}";
            if (edges.ContainsKey(k)) return;
            edges[k] = new EdgeAcc
            {
                Source = source,
                Rel = rel,
                FromType = fromType,
                FromId = fromId,
                ToType = toType,
                ToId = toId,
                IsBlocker = isBlocker,
            };
        }

        var focusKey = KeyOf(focusType.ToString(), focusId);
        nodes[focusKey] = new NodeAcc { Type = focusType.ToString(), Id = focusId, Tier = 0 };
        var frontier = new List<string> { focusKey };

        // The walk is bounded by `depth`, the `expanded` cycle guard, and the MaxNodes ceiling (which stops
        // AddNode from queueing new nodes) — NOT by `partial`. A single node's transient read failure or the
        // node ceiling flags `partial` truthfully but must not halt expansion of the other branches at this level.
        for (var level = 1; level <= depth; level++)
        {
            var next = new List<string>();
            foreach (var nodeKey in frontier)
            {
                if (!expanded.Add(nodeKey)) continue;
                var node = nodes[nodeKey];

                ArtifactRelationshipsDto rels;
                DependencyGraphEdges deps;
                try
                {
                    rels = Enum.TryParse<ArtifactType>(node.Type, out var at)
                        ? await fetchRelationships(at, node.Id, ct)
                        : new ArtifactRelationshipsDto(Array.Empty<RelationshipEdgeDto>(), Array.Empty<RelationshipEdgeDto>());
                    deps = DepEndpointNames.Contains(node.Type)
                        ? await fetchDependencies(node.Type, node.Id, ct)
                        : new DependencyGraphEdges(Array.Empty<DependencyGraphEdge>(), Array.Empty<DependencyGraphEdge>());
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    partial = true; // a failed node read is a leaf, not a blank graph
                    continue;
                }

                // Relationship edges. Outgoing → the far end is downstream (+level); incoming → upstream (−level).
                foreach (var e in rels.Outgoing)
                {
                    AddEdge("rel", e.Id, e.RelType, node.Type, node.Id, e.OtherType, e.OtherId, false);
                    var added = AddNode(e.OtherType, e.OtherId, e.OtherKey, e.OtherTitle, level);
                    if (added is not null) next.Add(added);
                }
                foreach (var e in rels.Incoming)
                {
                    AddEdge("rel", e.Id, e.RelType, e.OtherType, e.OtherId, node.Type, node.Id, false);
                    var added = AddNode(e.OtherType, e.OtherId, e.OtherKey, e.OtherTitle, -level);
                    if (added is not null) next.Add(added);
                }

                // Dependency edges. Outbound = this node is the From end (far end downstream); inbound = To end.
                foreach (var e in deps.Outbound)
                {
                    AddEdge("dep", e.Id, e.Kind, node.Type, node.Id, e.OtherType, e.OtherId, e.IsBlocker);
                    var added = AddNode(e.OtherType, e.OtherId, e.OtherKey, e.OtherTitle, level);
                    if (added is not null) next.Add(added);
                }
                foreach (var e in deps.Inbound)
                {
                    AddEdge("dep", e.Id, e.Kind, e.OtherType, e.OtherId, node.Type, node.Id, e.IsBlocker);
                    var added = AddNode(e.OtherType, e.OtherId, e.OtherKey, e.OtherTitle, -level);
                    if (added is not null) next.Add(added);
                }
            }
            frontier = next;
        }

        // Post-pass 1 — resolve stream codes for Topic nodes (FR-095 Topic-scope). A read failure degrades that
        // node to "no streams" (never cross-stream), flagged partial — never throws out of the graph.
        foreach (var n in nodes.Values.Where(n => n.Type == "Topic"))
        {
            try { n.Streams = (await fetchStreams(n.Id, ct)).ToArray(); }
            catch (Exception ex) when (ex is not OperationCanceledException) { partial = true; }
        }

        // Post-pass 2 — cross-stream per edge: both ends Topic, both stream sets non-empty and disjoint.
        foreach (var e in edges.Values)
        {
            if (e.FromType != "Topic" || e.ToType != "Topic") continue;
            if (!nodes.TryGetValue(KeyOf(e.FromType, e.FromId), out var f) ||
                !nodes.TryGetValue(KeyOf(e.ToType, e.ToId), out var t)) continue;
            if (f.Streams.Length == 0 || t.Streams.Length == 0) continue;
            e.IsCrossStream = !f.Streams.Intersect(t.Streams, StringComparer.Ordinal).Any();
        }

        // Post-pass 3 — a node is "blocked" if it touches any active blocker dependency edge.
        foreach (var e in edges.Values.Where(e => e.IsBlocker))
        {
            if (nodes.TryGetValue(KeyOf(e.FromType, e.FromId), out var f)) f.Blocked = true;
            if (nodes.TryGetValue(KeyOf(e.ToType, e.ToId), out var t)) t.Blocked = true;
        }

        var nodeDtos = nodes.Values
            .OrderBy(n => n.Tier).ThenBy(n => n.Key, StringComparer.Ordinal)
            .Select(n => new ImpactGraphNodeDto(n.Type, n.Id, n.Key, n.Title, n.Tier, n.Blocked, n.Streams))
            .ToList();
        // Drop any edge whose endpoint was rejected by the node ceiling — the contract never carries a
        // dangling edge (an edge to a node not in `nodes`), so PR2's layout never dereferences a missing node.
        var edgeDtos = edges.Values
            .Where(e => nodes.ContainsKey(KeyOf(e.FromType, e.FromId)) && nodes.ContainsKey(KeyOf(e.ToType, e.ToId)))
            .Select(e => new ImpactGraphEdgeDto(e.Source, e.Rel, e.FromType, e.FromId, e.ToType, e.ToId,
                e.IsBlocker, e.IsCrossStream))
            .ToList();

        return new ImpactGraphDto(focusType.ToString(), focusId, depth, nodeDtos, edgeDtos, partial);
    }
}
