using System.Net;
using System.Net.Http.Json;
using Acmp.Modules.Membership.Domain.Enums;
using FluentAssertions;

namespace Acmp.Api.Tests;

// HTTP-contract tests that exercise the request-body records in TopicEndpoints that are not yet
// reached by TopicApiTests: DeferTopicBody, PriorityBody, UpdateTopicBody, and the /prepare lambda.
public class TopicEndpointsCoverageTests
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

    private static object SubmitBody() => new
    {
        title = "Adopt Keycloak",
        description = "Consolidate IAM onto Keycloak.",
        justification = "Fragmented auth is risky.",
        type = "ArchitectureDecision",
        urgency = "Urgent",
        source = "CommitteeMember",
        streams = new[] { "identity" },
        systems = Array.Empty<string>(),
        tags = Array.Empty<string>(),
    };

    private sealed record SubmitResult(Guid Id, string Key);
    private sealed record MemberRow(Guid PublicId, string Role);

    // Shared setup: submit a topic as a Member, then accept it as Secretary (Submitted → Accepted).
    // AcceptTopicHandler calls BeginTriage() then Accept() in one shot (W2 + W3 rollup).
    // Secretary role satisfies Policies.TopicTriage directly (no ABAC needed).
    private static async Task<(Guid TopicId, AcmpWebApplicationFactory Factory)>
        CreateAcceptedTopicAsync()
    {
        var factory = new AcmpWebApplicationFactory();
        await factory.SeedMembersAsync(("kc-owner", "Owner One", CommitteeRole.Member));

        var topic = await (await Client(factory, "Member", sub: "kc-submitter")
            .PostAsJsonAsync("/api/topics", SubmitBody()))
            .Content.ReadFromJsonAsync<SubmitResult>();

        // GET /api/members is Secretary-only; we need the seeded Member's PublicId as OwnerId.
        var members = await (await Client(factory, "Secretary")
            .GetAsync("/api/members"))
            .Content.ReadFromJsonAsync<List<MemberRow>>();
        var owner = members!.First(m => m.Role == nameof(CommitteeRole.Member));

        await Client(factory, "Secretary", sub: "kc-sec").PostAsJsonAsync(
            $"/api/topics/{topic!.Id}/accept",
            new { ownerId = owner.PublicId, ownerName = "Owner One" });

        return (topic.Id, factory);
    }

    // ---- DeferTopicBody ----------------------------------------------------------------

    [Fact] // FluentValidation: Reason.NotEmpty → 400; covers DeferTopicBody binding
    public async Task Defer_empty_reason_returns_400()
    {
        var (topicId, factory) = await CreateAcceptedTopicAsync();
        await using (factory)
        {
            var response = await Client(factory, "Secretary").PostAsJsonAsync(
                $"/api/topics/{topicId}/defer",
                new { reason = "", revisitOn = (DateTimeOffset?)null });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }

    [Fact] // W20: DeferTopicBody happy path — Accepted topic deferred with reason → 204
    public async Task Secretary_defers_accepted_topic_returns_204()
    {
        var (topicId, factory) = await CreateAcceptedTopicAsync();
        await using (factory)
        {
            var response = await Client(factory, "Secretary").PostAsJsonAsync(
                $"/api/topics/{topicId}/defer",
                new { reason = "Pending architecture review.", revisitOn = (DateTimeOffset?)DateTimeOffset.Parse("2026-09-01T00:00:00Z") });

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
    }

    // ---- /prepare lambda ---------------------------------------------------------------

    [Fact] // W4: /prepare lambda — Secretary has TopicEdit directly; Accepted → Prepared → 204
    public async Task Secretary_prepares_accepted_topic_returns_204()
    {
        var (topicId, factory) = await CreateAcceptedTopicAsync();
        await using (factory)
        {
            // No RequireAuthorization policy on /prepare; handler gates via IResourceAuthorizer
            // with Policies.TopicEdit which grants Secretary/Chairman without ABAC ownership.
            var response = await Client(factory, "Secretary", sub: "kc-sec")
                .PostAsync($"/api/topics/{topicId}/prepare", null);

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
    }

    // ---- PriorityBody ------------------------------------------------------------------

    [Fact] // docs/10: BacklogPrioritize is Chairman/Secretary only; Member is forbidden → 403
    public async Task Member_cannot_set_priority_returns_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var topic = await (await Client(factory, "Member", sub: "kc-omar")
            .PostAsJsonAsync("/api/topics", SubmitBody()))
            .Content.ReadFromJsonAsync<SubmitResult>();

        var response = await Client(factory, "Member")
            .PutAsJsonAsync($"/api/topics/{topic!.Id}/priority", new { priority = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // W3: PriorityBody happy path — Secretary sets priority → 204
    public async Task Secretary_sets_topic_priority_returns_204()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var topic = await (await Client(factory, "Member", sub: "kc-omar")
            .PostAsJsonAsync("/api/topics", SubmitBody()))
            .Content.ReadFromJsonAsync<SubmitResult>();

        var response = await Client(factory, "Secretary")
            .PutAsJsonAsync($"/api/topics/{topic!.Id}/priority", new { priority = 2 });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ---- UpdateTopicBody ---------------------------------------------------------------

    [Fact] // AC-034: submitter updates own Submitted topic (SubmittedBySub == current user →
           // no ABAC check needed); covers all UpdateTopicBody fields → 204
    public async Task Submitter_updates_own_topic_returns_204()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var topic = await (await Client(factory, "Member", sub: "kc-submitter")
            .PostAsJsonAsync("/api/topics", SubmitBody()))
            .Content.ReadFromJsonAsync<SubmitResult>();

        var response = await Client(factory, "Member", sub: "kc-submitter").PutAsJsonAsync(
            $"/api/topics/{topic!.Id}",
            new
            {
                title = "Adopt Keycloak — Revised",
                description = "Updated description.",
                justification = "Clearer justification.",
                urgency = "Normal",
                streams = new[] { "identity" },
                systems = Array.Empty<string>(),
                tags = Array.Empty<string>(),
            });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
