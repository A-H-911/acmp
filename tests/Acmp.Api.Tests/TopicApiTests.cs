using System.Net;
using System.Net.Http.Json;
using Acmp.Modules.Membership.Domain.Enums;
using FluentAssertions;

namespace Acmp.Api.Tests;

// HTTP-contract tests for /api/topics through the real pipeline + policy authorization + ABAC.
public class TopicApiTests
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

    private static object SubmitBody(params string[] streams) => new
    {
        title = "Adopt Keycloak",
        description = "Consolidate IAM onto Keycloak.",
        justification = "Fragmented auth is risky.",
        type = "ArchitectureDecision",
        urgency = "Urgent",
        source = "CommitteeMember",
        streams,
        systems = Array.Empty<string>(),
        tags = Array.Empty<string>(),
    };

    private sealed record SubmitResult(Guid Id, string Key);
    private sealed record TopicRow(string Key, string Title, string Status);
    private sealed record Backlog(List<TopicRow> Items, int Total);
    private sealed record MemberRow(Guid PublicId, string Role);

    [Fact] // AC-008
    public async Task Submit_without_token_returns_401()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, roles: null).PostAsJsonAsync("/api/topics", SubmitBody("identity"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // AC-005/006: Auditor is not in Topic.Submit
    public async Task Auditor_cannot_submit_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, "Auditor").PostAsJsonAsync("/api/topics", SubmitBody("identity"));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // AC-030
    public async Task Submit_without_a_stream_returns_400()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, "Member").PostAsJsonAsync("/api/topics", SubmitBody());
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact] // W1 + backlog + detail round-trip over HTTP
    public async Task Submit_then_read_backlog_and_detail()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var member = Client(factory, "Member", sub: "kc-omar");

        var submit = await member.PostAsJsonAsync("/api/topics", SubmitBody("identity", "platform"));
        submit.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await submit.Content.ReadFromJsonAsync<SubmitResult>();
        result!.Key.Should().Be("TOP-2026-001");

        var backlog = await (await member.GetAsync("/api/topics")).Content.ReadFromJsonAsync<Backlog>();
        backlog!.Total.Should().Be(1);
        backlog.Items[0].Key.Should().Be(result.Key);

        var detail = await member.GetAsync($"/api/topics/{result.Key}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);

        var missing = await member.GetAsync("/api/topics/TOP-2026-999");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact] // W2: triage authorization (Member 403, Secretary 204) + grant-on-accept
    public async Task Only_secretary_can_accept_a_topic()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedMembersAsync(("kc-owner", "Owner One", CommitteeRole.Member));

        var submit = await Client(factory, "Member", sub: "kc-omar").PostAsJsonAsync("/api/topics", SubmitBody("identity"));
        var topic = await submit.Content.ReadFromJsonAsync<SubmitResult>();

        var owner = (await (await Client(factory, "Secretary").GetAsync("/api/members"))
            .Content.ReadFromJsonAsync<List<MemberRow>>())!.Single(m => m.Role == nameof(CommitteeRole.Member));
        var body = new { ownerId = owner.PublicId, ownerName = "Owner One" };

        var asMember = await Client(factory, "Member").PostAsJsonAsync($"/api/topics/{topic!.Id}/accept", body);
        asMember.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var asSecretary = await Client(factory, "Secretary", sub: "kc-sec").PostAsJsonAsync($"/api/topics/{topic.Id}/accept", body);
        asSecretary.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact] // AC-031
    public async Task Reject_without_a_reason_returns_400()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var submit = await Client(factory, "Member", sub: "kc-omar").PostAsJsonAsync("/api/topics", SubmitBody("identity"));
        var topic = await submit.Content.ReadFromJsonAsync<SubmitResult>();

        var response = await Client(factory, "Secretary").PostAsJsonAsync($"/api/topics/{topic!.Id}/reject", new { reason = "" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact] // BL-033: comment by any authenticated member
    public async Task Member_can_comment_on_a_topic()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var member = Client(factory, "Member", sub: "kc-omar");
        var submit = await member.PostAsJsonAsync("/api/topics", SubmitBody("identity"));
        var topic = await submit.Content.ReadFromJsonAsync<SubmitResult>();

        var response = await member.PostAsJsonAsync($"/api/topics/{topic!.Id}/comments", new { reason = "Agreed; document rollback." });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
