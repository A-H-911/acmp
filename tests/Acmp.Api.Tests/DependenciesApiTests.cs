using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Acmp.Api.Tests;

// HTTP-contract tests for /api/dependencies through the real pipeline + policy authorization (docs/10).
// Reads are committee-wide; create/resolve/remove are Dependency.Create (Chairman/Secretary). The acting
// subject/roles are set per request via the test auth header, so the RBAC narrowing is exercised end to end.
public class DependenciesApiTests
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

    private static object Body(Guid? fromId = null, Guid? toId = null, string kind = "BlockedBy") => new
    {
        fromType = "Topic",
        fromId = fromId ?? Guid.NewGuid(),
        fromKey = "TOP-2026-042",
        fromTitle = "API Gateway migration",
        toType = "Action",
        toId = toId ?? Guid.NewGuid(),
        toKey = "ACT-2026-009",
        toTitle = "Rotate keys",
        kind,
        note = (string?)null,
    };

    private sealed record Created(string Key);
    private sealed record Detail(Guid Id, string Key, string Kind, string Status, bool IsBlocker);
    private sealed record Edge(Guid Id, string Key, string OtherType, Guid OtherId, string OtherKey, string OtherTitle, string Kind, string Status, bool IsBlocker);
    private sealed record Panel(List<Edge> Outbound, List<Edge> Inbound);
    private sealed record Page(int Total);

    private static async Task<Detail> CreateAsync(HttpClient c, Guid? fromId = null, Guid? toId = null, string kind = "BlockedBy")
    {
        var create = await c.PostAsJsonAsync("/api/dependencies", Body(fromId, toId, kind));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var key = (await create.Content.ReadFromJsonAsync<Created>())!.Key;
        return (await (await c.GetAsync($"/api/dependencies/{key}")).Content.ReadFromJsonAsync<Detail>())!;
    }

    [Fact] // AC-008: no token → 401
    public async Task Create_without_token_returns_401()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, roles: null).PostAsJsonAsync("/api/dependencies", Body())).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // Dependency.Create denies Auditor
    public async Task Auditor_cannot_create_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, "Auditor").PostAsJsonAsync("/api/dependencies", Body())).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // A self-loop is rejected at the validator (400)
    public async Task Create_self_loop_returns_400()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var id = Guid.NewGuid();
        var body = new
        {
            fromType = "Topic",
            fromId = id,
            fromKey = "TOP-1",
            fromTitle = "T",
            toType = "Topic",
            toId = id,
            toKey = "TOP-1",
            toTitle = "T",
            kind = "BlockedBy",
            note = (string?)null,
        };
        (await Client(factory, "Secretary", "kc-sec").PostAsJsonAsync("/api/dependencies", body)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact] // W: Secretary creates → detail → register → resolve
    public async Task Secretary_creates_reads_and_resolves()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "kc-sec");

        var detail = await CreateAsync(sec);
        detail.Key.Should().Be("DPN-2026-001");
        detail.Status.Should().Be("Open");
        detail.IsBlocker.Should().BeTrue();

        (await sec.GetAsync("/api/dependencies/DPN-2026-999")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var page = await (await sec.GetAsync("/api/dependencies")).Content.ReadFromJsonAsync<Page>();
        page!.Total.Should().BeGreaterThanOrEqualTo(1);

        (await sec.PostAsync($"/api/dependencies/{detail.Id}/resolve", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var resolved = await (await sec.GetAsync($"/api/dependencies/{detail.Key}")).Content.ReadFromJsonAsync<Detail>();
        resolved!.Status.Should().Be("Resolved");
        resolved.IsBlocker.Should().BeFalse();   // Resolved is never a blocker
    }

    [Fact]
    public async Task Remove_soft_deletes_and_leaves_the_register()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "kc-sec");
        var detail = await CreateAsync(sec);

        (await sec.PostAsync($"/api/dependencies/{detail.Id}/remove", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var page = await (await sec.GetAsync("/api/dependencies")).Content.ReadFromJsonAsync<Page>();
        page!.Total.Should().Be(0);   // Removed excluded by default
    }

    [Fact]
    public async Task Resolve_unknown_returns_404()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, "Secretary", "kc-sec").PostAsync($"/api/dependencies/{Guid.NewGuid()}/resolve", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Remove_unknown_returns_404()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, "Secretary", "kc-sec").PostAsync($"/api/dependencies/{Guid.NewGuid()}/remove", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact] // The panel splits outbound/inbound; a Member (read-only) can view it.
    public async Task Artifact_panel_shows_outbound_and_inbound()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "kc-sec");
        var topicId = Guid.NewGuid();

        await CreateAsync(sec, fromId: topicId);              // topic --BlockedBy--> action (outbound for topic)

        var member = Client(factory, "Member", "kc-mem");
        var panel = await (await member.GetAsync($"/api/dependencies/artifact/Topic/{topicId}")).Content.ReadFromJsonAsync<Panel>();
        panel!.Outbound.Should().ContainSingle();
        panel.Outbound[0].OtherType.Should().Be("Action");
        panel.Outbound[0].OtherKey.Should().Be("ACT-2026-009");
        panel.Inbound.Should().BeEmpty();
    }
}
