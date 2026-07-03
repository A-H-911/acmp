using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Acmp.Api.Tests;

// HTTP-contract tests for /api/traceability through the real pipeline + policy authorization (docs/10,
// docs/30 §6.1). The panel read is committee-wide; create/deactivate a typed edge is Traceability.Link
// (Chairman/Secretary). AC-062 (panel up/downstream) + AC-063 (create → both panels show it, audited).
public class TraceabilityApiTests
{
    private static HttpClient Client(AcmpWebApplicationFactory factory, string? roles, string sub = "u1")
    {
        var client = factory.CreateClient();
        if (roles is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
            client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        }
        return client;
    }

    private static object EdgeBody(Guid sourceId, Guid targetId, string rel = "DecidedBy") => new
    {
        sourceType = "Topic",
        sourceId,
        sourceKey = "TOP-2026-042",
        sourceTitle = "API Gateway migration",
        targetType = "Decision",
        targetId,
        targetKey = "DECN-2026-007",
        targetTitle = "Approve gateway",
        relType = rel,
        notes = (string?)null,
    };

    private sealed record CreatedEdge(Guid Id);
    private sealed record EdgeRow(Guid Id, string RelType, string Direction, string OtherType, Guid OtherId, string OtherKey, string OtherTitle, string? Notes);
    private sealed record Panel(List<EdgeRow> Outgoing, List<EdgeRow> Incoming);

    [Fact] // AC-008: no token → 401
    public async Task Panel_without_token_returns_401()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, roles: null).GetAsync($"/api/traceability/Topic/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // docs/30 §6.1: Member is read-only — creating an edge is forbidden
    public async Task Member_cannot_create_an_edge_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, "Member").PostAsJsonAsync("/api/traceability", EdgeBody(Guid.NewGuid(), Guid.NewGuid()));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // A self-loop is rejected at the validator (400)
    public async Task Create_self_loop_returns_400()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var id = Guid.NewGuid();
        var body = new
        {
            sourceType = "Topic",
            sourceId = id,
            sourceKey = "TOP-1",
            sourceTitle = "T",
            targetType = "Topic",
            targetId = id,
            targetKey = "TOP-1",
            targetTitle = "T",
            relType = "DependsOn",
            notes = (string?)null,
        };
        var response = await Client(factory, "Secretary", "kc-sec").PostAsJsonAsync("/api/traceability", body);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact] // AC-063: a Secretary creates an edge; it shows OUTGOING on the source panel and INCOMING on the target.
    public async Task Secretary_creates_an_edge_and_both_panels_show_it()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "kc-sec");
        var topicId = Guid.NewGuid();
        var decisionId = Guid.NewGuid();

        var create = await sec.PostAsJsonAsync("/api/traceability", EdgeBody(topicId, decisionId));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        (await create.Content.ReadFromJsonAsync<CreatedEdge>())!.Id.Should().NotBeEmpty();

        // Member (read-only) can view the panel.
        var member = Client(factory, "Member", "kc-mem");
        var topicPanel = await (await member.GetAsync($"/api/traceability/Topic/{topicId}")).Content.ReadFromJsonAsync<Panel>();
        topicPanel!.Outgoing.Should().ContainSingle();
        topicPanel.Incoming.Should().BeEmpty();
        topicPanel.Outgoing[0].OtherType.Should().Be("Decision");
        topicPanel.Outgoing[0].OtherKey.Should().Be("DECN-2026-007");
        topicPanel.Outgoing[0].RelType.Should().Be("DecidedBy");

        var decisionPanel = await (await member.GetAsync($"/api/traceability/Decision/{decisionId}")).Content.ReadFromJsonAsync<Panel>();
        decisionPanel!.Incoming.Should().ContainSingle();
        decisionPanel.Incoming[0].Direction.Should().Be("Incoming");
        decisionPanel.Incoming[0].OtherKey.Should().Be("TOP-2026-042");
    }

    [Fact] // A deactivated edge disappears from the panel (soft-delete, docs/30 §5).
    public async Task Deactivated_edge_leaves_the_panel()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "kc-sec");
        var topicId = Guid.NewGuid();

        var id = (await (await sec.PostAsJsonAsync("/api/traceability", EdgeBody(topicId, Guid.NewGuid())))
            .Content.ReadFromJsonAsync<CreatedEdge>())!.Id;

        var deactivate = await sec.PostAsync($"/api/traceability/{id}/deactivate", null);
        deactivate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var panel = await (await sec.GetAsync($"/api/traceability/Topic/{topicId}")).Content.ReadFromJsonAsync<Panel>();
        panel!.Outgoing.Should().BeEmpty();
    }

    [Fact] // Deactivating an unknown edge → 404
    public async Task Deactivate_unknown_returns_404()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, "Secretary", "kc-sec").PostAsync($"/api/traceability/{Guid.NewGuid()}/deactivate", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- FR-096 impact graph (P10f) ----------------------------------------------------------------------

    private sealed record GNode(string Type, Guid Id, string Key, string Title, int Tier, bool Blocked, List<string> Streams);
    private sealed record GEdge(string Source, string Rel, string FromType, Guid FromId, string ToType, Guid ToId, bool IsBlocker, bool IsCrossStream);
    private sealed record Graph(string FocusType, Guid FocusId, int Depth, List<GNode> Nodes, List<GEdge> Edges, bool Partial);

    [Fact] // AC-008: no token → 401
    public async Task Graph_without_token_returns_401()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, roles: null).GetAsync($"/api/traceability/graph/Topic/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // FR-096: a real end-to-end walk — Topic→Decision→Action composes into signed tiers over 2 hops.
    public async Task Graph_composes_relationship_edges_into_tiers()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "kc-sec");
        var topicId = Guid.NewGuid();
        var decisionId = Guid.NewGuid();
        var actionId = Guid.NewGuid();

        // Topic --DecidedBy--> Decision --RecordedAs--> Action
        await sec.PostAsJsonAsync("/api/traceability", EdgeBody(topicId, decisionId));
        await sec.PostAsJsonAsync("/api/traceability", new
        {
            sourceType = "Decision",
            sourceId = decisionId,
            sourceKey = "DECN-2026-007",
            sourceTitle = "Approve gateway",
            targetType = "Action",
            targetId = actionId,
            targetKey = "ACT-2026-010",
            targetTitle = "Do the migration",
            relType = "RecordedAs",
            notes = (string?)null,
        });

        var graph = await (await sec.GetAsync($"/api/traceability/graph/Topic/{topicId}?depth=2"))
            .Content.ReadFromJsonAsync<Graph>();

        graph!.FocusType.Should().Be("Topic");
        graph.Depth.Should().Be(2);
        graph.Nodes.Should().HaveCount(3);
        graph.Nodes.Single(n => n.Id == topicId).Tier.Should().Be(0);
        graph.Nodes.Single(n => n.Id == decisionId).Tier.Should().Be(1);
        graph.Nodes.Single(n => n.Id == actionId).Tier.Should().Be(2);
        graph.Nodes.Single(n => n.Id == actionId).Key.Should().Be("ACT-2026-010");
        graph.Edges.Should().HaveCount(2);
        graph.Partial.Should().BeFalse();
    }

    [Fact] // Depth 1 stops one hop out — the Action (2 hops) is not in the graph.
    public async Task Graph_depth_one_stops_at_first_hop()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "kc-sec");
        var topicId = Guid.NewGuid();
        var decisionId = Guid.NewGuid();
        var actionId = Guid.NewGuid();
        await sec.PostAsJsonAsync("/api/traceability", EdgeBody(topicId, decisionId));
        await sec.PostAsJsonAsync("/api/traceability", new
        {
            sourceType = "Decision",
            sourceId = decisionId,
            sourceKey = "DECN-2026-007",
            sourceTitle = "Approve",
            targetType = "Action",
            targetId = actionId,
            targetKey = "ACT-2026-010",
            targetTitle = "Do it",
            relType = "RecordedAs",
            notes = (string?)null,
        });

        var graph = await (await sec.GetAsync($"/api/traceability/graph/Topic/{topicId}?depth=1"))
            .Content.ReadFromJsonAsync<Graph>();

        graph!.Nodes.Select(n => n.Id).Should().NotContain(actionId);
    }
}
