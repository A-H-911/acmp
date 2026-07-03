using Acmp.Modules.Traceability.Application.Contracts;
using Acmp.Modules.Traceability.Application.Features.GetArtifactRelationships;
using Acmp.Modules.Traceability.Application.Features.GetImpactGraph;
using Acmp.Modules.Traceability.Domain.Enums;
using Acmp.Shared.Contracts.Dependencies;
using Acmp.Shared.Contracts.Topics;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Acmp.Application.Tests.Traceability;

// Exercises the P10f impact-graph composer through its public handler (FR-096): the BFS tiers, the
// relationship+dependency union, depth bounding + clamp, the cycle guard, the node ceiling, per-node partial
// failure, the System dead-end + unmapped-type skip, blocked derivation, and the FR-095 Topic-scope
// cross-stream math. Pure — no DB: ISender + the two Acmp.Shared ports are faked from in-memory edge maps.
public class ImpactGraphTests
{
    // ---- in-memory graph fixture -------------------------------------------------------------------------

    private sealed class Fixture
    {
        public readonly Dictionary<(string Type, Guid Id), ArtifactRelationshipsDto> Rels = new();
        public readonly Dictionary<(string Type, Guid Id), DependencyGraphEdges> Deps = new();
        public readonly Dictionary<Guid, IReadOnlyList<string>> Streams = new();
        public readonly HashSet<(string Type, Guid Id)> RelThrows = new();
        public readonly HashSet<Guid> StreamThrows = new();
        public readonly HashSet<string> DepCalledFor = new();
        public readonly HashSet<string> RelCalledFor = new();

        public GetImpactGraphHandler Build()
        {
            var sender = Substitute.For<ISender>();
            sender.Send(Arg.Any<GetArtifactRelationshipsQuery>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var q = (GetArtifactRelationshipsQuery)ci[0]!;
                    RelCalledFor.Add(q.Type.ToString());
                    if (RelThrows.Contains((q.Type.ToString(), q.Id))) throw new InvalidOperationException("boom");
                    return Task.FromResult(Rels.TryGetValue((q.Type.ToString(), q.Id), out var d)
                        ? d : EmptyRels());
                });

            var deps = Substitute.For<IDependencyArtifactReader>();
            deps.GetForArtifactAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var type = (string)ci[0]!;
                    var id = (Guid)ci[1]!;
                    DepCalledFor.Add(type);
                    return Task.FromResult(Deps.TryGetValue((type, id), out var d) ? d : EmptyDeps());
                });

            var streams = Substitute.For<ITopicStreamReader>();
            streams.GetStreamsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var id = (Guid)ci[0]!;
                    if (StreamThrows.Contains(id)) throw new InvalidOperationException("stream boom");
                    return Task.FromResult(Streams.TryGetValue(id, out var s) ? s : (IReadOnlyList<string>)Array.Empty<string>());
                });

            return new GetImpactGraphHandler(sender, deps, streams);
        }
    }

    private static ArtifactRelationshipsDto EmptyRels() =>
        new(Array.Empty<RelationshipEdgeDto>(), Array.Empty<RelationshipEdgeDto>());

    private static DependencyGraphEdges EmptyDeps() =>
        new(Array.Empty<DependencyGraphEdge>(), Array.Empty<DependencyGraphEdge>());

    private static RelationshipEdgeDto Rel(RelationshipType rel, string dir, ArtifactType otherType, Guid otherId,
        string key = "X", string title = "t") =>
        new(Guid.NewGuid(), rel.ToString(), dir, otherType.ToString(), otherId, key, title, null);

    private static DependencyGraphEdge Dep(string kind, string otherType, Guid otherId, bool blocker,
        string key = "DPN", string title = "t") =>
        new(Guid.NewGuid(), key, otherType, otherId, "K", title, kind, "Open", blocker);

    private static Task<ImpactGraphDto> Run(Fixture f, ArtifactType type, Guid id, int depth) =>
        f.Build().Handle(new GetImpactGraphQuery(type, id, depth), CancellationToken.None);

    // ---- traversal + tiers -------------------------------------------------------------------------------

    [Fact] // Focus is tier 0; an outgoing edge is downstream (+1), an incoming edge is upstream (−1).
    public async Task Builds_signed_tiers_around_the_focus()
    {
        var f = new Fixture();
        var focus = Guid.NewGuid();
        var decision = Guid.NewGuid();
        var upstreamTopic = Guid.NewGuid();
        f.Rels[("Topic", focus)] = new(
            new[] { Rel(RelationshipType.DecidedBy, "Outgoing", ArtifactType.Decision, decision, "DECN-1", "Approve") },
            new[] { Rel(RelationshipType.DependsOn, "Incoming", ArtifactType.Topic, upstreamTopic, "TOP-9", "Upstream") });

        var g = await Run(f, ArtifactType.Topic, focus, 1);

        g.Nodes.Should().HaveCount(3);
        g.Nodes.Single(n => n.Id == focus).Tier.Should().Be(0);
        g.Nodes.Single(n => n.Id == decision).Tier.Should().Be(1);
        g.Nodes.Single(n => n.Id == decision).Key.Should().Be("DECN-1"); // snapshot carried off the edge
        g.Nodes.Single(n => n.Id == upstreamTopic).Tier.Should().Be(-1);
        g.Edges.Should().HaveCount(2);
        g.Partial.Should().BeFalse();
        g.FocusType.Should().Be("Topic");
    }

    [Fact] // The focus node carries no key/title (its identity is not on any edge — the SPA supplies it).
    public async Task Focus_node_has_empty_key_and_title()
    {
        var f = new Fixture();
        var focus = Guid.NewGuid();
        var g = await Run(f, ArtifactType.Topic, focus, 2);

        var node = g.Nodes.Single();
        node.Tier.Should().Be(0);
        node.Key.Should().BeEmpty();
        node.Title.Should().BeEmpty();
        g.Edges.Should().BeEmpty();
        g.Partial.Should().BeFalse();
    }

    [Fact] // Depth bounds the walk: depth 1 sees the neighbour but not the neighbour's neighbour.
    public async Task Depth_bounds_the_traversal()
    {
        var f = new Fixture();
        var focus = Guid.NewGuid();
        var mid = Guid.NewGuid();
        var far = Guid.NewGuid();
        f.Rels[("Topic", focus)] = new(new[] { Rel(RelationshipType.DecidedBy, "Outgoing", ArtifactType.Decision, mid) }, Array.Empty<RelationshipEdgeDto>());
        f.Rels[("Decision", mid)] = new(new[] { Rel(RelationshipType.RecordedAs, "Outgoing", ArtifactType.Action, far) }, Array.Empty<RelationshipEdgeDto>());

        var d1 = await Run(f, ArtifactType.Topic, focus, 1);
        var d2 = await Run(f, ArtifactType.Topic, focus, 2);

        d1.Nodes.Select(n => n.Id).Should().NotContain(far);
        d2.Nodes.Select(n => n.Id).Should().Contain(far);
    }

    [Fact] // Depth is clamped to 1..3 (a 0 becomes 1, a 5 becomes 3) — never an unbounded or empty walk.
    public async Task Depth_is_clamped()
    {
        var f = new Fixture();
        var focus = Guid.NewGuid();
        (await Run(f, ArtifactType.Topic, focus, 0)).Depth.Should().Be(1);
        (await Run(f, ArtifactType.Topic, focus, 9)).Depth.Should().Be(3);
    }

    [Fact] // A cycle (A→B, B→A) terminates and visits each node once.
    public async Task Cycle_terminates_without_reexpansion()
    {
        var f = new Fixture();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        f.Rels[("Topic", a)] = new(new[] { Rel(RelationshipType.References, "Outgoing", ArtifactType.Topic, b) }, Array.Empty<RelationshipEdgeDto>());
        f.Rels[("Topic", b)] = new(new[] { Rel(RelationshipType.References, "Outgoing", ArtifactType.Topic, a) }, Array.Empty<RelationshipEdgeDto>());

        var g = await Run(f, ArtifactType.Topic, a, 3);

        g.Nodes.Should().HaveCount(2);
        g.Nodes.Select(n => n.Id).Should().BeEquivalentTo(new[] { a, b });
    }

    [Fact] // The same edge reached from both endpoints is emitted once.
    public async Task Edges_are_deduplicated()
    {
        var f = new Fixture();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var shared = new RelationshipEdgeDto(Guid.NewGuid(), "DecidedBy", "Outgoing", "Decision", b, "DECN-1", "t", null);
        f.Rels[("Topic", a)] = new(new[] { shared }, Array.Empty<RelationshipEdgeDto>());
        f.Rels[("Decision", b)] = new(Array.Empty<RelationshipEdgeDto>(),
            new[] { new RelationshipEdgeDto(shared.Id, "DecidedBy", "Incoming", "Topic", a, "TOP-1", "t", null) });

        var g = await Run(f, ArtifactType.Topic, a, 2);

        g.Edges.Should().HaveCount(1);
    }

    // ---- dependency union + blocked ----------------------------------------------------------------------

    [Fact] // Dependency edges union into the graph; a blocker edge marks its endpoints blocked.
    public async Task Unions_dependency_edges_and_marks_blocked()
    {
        var f = new Fixture();
        var focus = Guid.NewGuid();
        var action = Guid.NewGuid();
        f.Deps[("Topic", focus)] = new(new[] { Dep("Blocks", "Action", action, blocker: true, "DPN-1", "Do it") }, Array.Empty<DependencyGraphEdge>());

        var g = await Run(f, ArtifactType.Topic, focus, 1);

        var edge = g.Edges.Should().ContainSingle().Subject;
        edge.Source.Should().Be("dep");
        edge.Rel.Should().Be("Blocks");
        edge.IsBlocker.Should().BeTrue();
        g.Nodes.Single(n => n.Id == action).Blocked.Should().BeTrue();
        g.Nodes.Single(n => n.Id == action).Tier.Should().Be(1); // outbound → downstream
    }

    [Fact] // An inbound dependency edge places the far end upstream (−1).
    public async Task Inbound_dependency_is_upstream()
    {
        var f = new Fixture();
        var focus = Guid.NewGuid();
        var other = Guid.NewGuid();
        f.Deps[("Action", focus)] = new(Array.Empty<DependencyGraphEdge>(), new[] { Dep("DependsOn", "Topic", other, blocker: false) });

        var g = await Run(f, ArtifactType.Action, focus, 1);

        g.Nodes.Single(n => n.Id == other).Tier.Should().Be(-1);
    }

    // ---- dual-enum: System dead-end + unmapped-type skip -------------------------------------------------

    [Fact] // A System node (dependency-only, not an ArtifactType) never gets a relationship read.
    public async Task System_node_skips_the_relationship_read()
    {
        var f = new Fixture();
        var focus = Guid.NewGuid();
        var system = Guid.NewGuid();
        f.Deps[("Topic", focus)] = new(new[] { Dep("RelatesTo", "System", system, blocker: false) }, Array.Empty<DependencyGraphEdge>());

        var g = await Run(f, ArtifactType.Topic, focus, 2);

        g.Nodes.Select(n => n.Id).Should().Contain(system);
        f.RelCalledFor.Should().NotContain("System"); // no /traceability/System/... call — would 404 in prod
        f.DepCalledFor.Should().Contain("System");     // System still expands via dependency edges
        g.Partial.Should().BeFalse();
    }

    [Fact] // A relationship-only type with no dependency endpoint (e.g. Adr) skips the dependency read.
    public async Task Unmapped_type_skips_the_dependency_read()
    {
        var f = new Fixture();
        var focus = Guid.NewGuid();
        var adr = Guid.NewGuid();
        f.Rels[("Decision", focus)] = new(new[] { Rel(RelationshipType.RecordedAs, "Outgoing", ArtifactType.Adr, adr) }, Array.Empty<RelationshipEdgeDto>());

        var g = await Run(f, ArtifactType.Decision, focus, 2);

        g.Nodes.Select(n => n.Id).Should().Contain(adr);
        f.DepCalledFor.Should().NotContain("Adr"); // Adr has no dependency endpoint — no wasted read
    }

    // ---- partial failure ---------------------------------------------------------------------------------

    [Fact] // A failed node read degrades that node to a leaf and flags partial — never blanks the graph.
    public async Task Failed_node_read_is_partial_not_blank()
    {
        var f = new Fixture();
        var focus = Guid.NewGuid();
        var mid = Guid.NewGuid();
        f.Rels[("Topic", focus)] = new(new[] { Rel(RelationshipType.DecidedBy, "Outgoing", ArtifactType.Decision, mid) }, Array.Empty<RelationshipEdgeDto>());
        f.RelThrows.Add(("Decision", mid)); // expanding mid throws

        var g = await Run(f, ArtifactType.Topic, focus, 2);

        g.Partial.Should().BeTrue();
        g.Nodes.Select(n => n.Id).Should().Contain(mid); // still present, just not expanded
    }

    // ---- FR-095 Topic-scope cross-stream -----------------------------------------------------------------

    [Fact] // Two Topics with disjoint non-empty streams → the edge is cross-stream.
    public async Task Cross_stream_when_topic_streams_are_disjoint()
    {
        var f = new Fixture();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        f.Rels[("Topic", a)] = new(new[] { Rel(RelationshipType.DependsOn, "Outgoing", ArtifactType.Topic, b) }, Array.Empty<RelationshipEdgeDto>());
        f.Streams[a] = new[] { "identity" };
        f.Streams[b] = new[] { "payments" };

        var g = await Run(f, ArtifactType.Topic, a, 1);

        g.Edges.Single().IsCrossStream.Should().BeTrue();
        g.Nodes.Single(n => n.Id == a).Streams.Should().Contain("identity");
    }

    [Fact] // Shared stream, an empty stream set, or a non-Topic endpoint are all NOT cross-stream.
    public async Task Not_cross_stream_when_shared_empty_or_non_topic()
    {
        var f = new Fixture();
        var a = Guid.NewGuid();
        var shared = Guid.NewGuid();
        var noStream = Guid.NewGuid();
        var action = Guid.NewGuid();
        f.Rels[("Topic", a)] = new(new[]
        {
            Rel(RelationshipType.References, "Outgoing", ArtifactType.Topic, shared, "TOP-S"),
            Rel(RelationshipType.References, "Outgoing", ArtifactType.Topic, noStream, "TOP-N"),
            Rel(RelationshipType.Produces, "Outgoing", ArtifactType.Action, action, "ACT-1"),
        }, Array.Empty<RelationshipEdgeDto>());
        f.Streams[a] = new[] { "identity" };
        f.Streams[shared] = new[] { "identity" };  // same stream
        // noStream: no entry → empty

        var g = await Run(f, ArtifactType.Topic, a, 1);

        g.Edges.Should().OnlyContain(e => e.IsCrossStream == false);
    }

    [Fact] // A stream read failure flags partial and leaves that node with no streams (not cross-stream).
    public async Task Stream_read_failure_is_partial()
    {
        var f = new Fixture();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        f.Rels[("Topic", a)] = new(new[] { Rel(RelationshipType.DependsOn, "Outgoing", ArtifactType.Topic, b) }, Array.Empty<RelationshipEdgeDto>());
        f.Streams[a] = new[] { "identity" };
        f.StreamThrows.Add(b);

        var g = await Run(f, ArtifactType.Topic, a, 1);

        g.Partial.Should().BeTrue();
        g.Edges.Single().IsCrossStream.Should().BeFalse();
    }

    // ---- node ceiling ------------------------------------------------------------------------------------

    [Fact] // Pathological fan-out is capped: partial is flagged and the node set stays bounded.
    public async Task Node_ceiling_caps_fan_out()
    {
        var f = new Fixture();
        var focus = Guid.NewGuid();
        var fanout = Enumerable.Range(0, 200)
            .Select(_ => Rel(RelationshipType.Produces, "Outgoing", ArtifactType.Action, Guid.NewGuid()))
            .ToArray();
        f.Rels[("Topic", focus)] = new(fanout, Array.Empty<RelationshipEdgeDto>());

        var g = await Run(f, ArtifactType.Topic, focus, 1);

        g.Partial.Should().BeTrue();
        g.Nodes.Count.Should().BeLessThanOrEqualTo(ImpactGraphMaxNodesProbe.Value + 1);

        // Integrity: every emitted edge connects two nodes that ARE in the response — the ceiling must never
        // leave a dangling edge (an edge to a rejected node), which would NaN the PR2 SVG layout.
        var nodeIds = g.Nodes.Select(n => n.Id).ToHashSet();
        g.Edges.Should().OnlyContain(e => nodeIds.Contains(e.FromId) && nodeIds.Contains(e.ToId));
    }
}

// Keeps the ceiling assertion honest without making the composer public — mirrors the const it tests.
internal static class ImpactGraphMaxNodesProbe
{
    public const int Value = 60;
}
