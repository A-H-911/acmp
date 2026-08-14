using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
        streams = new[] { "core" },
        systems = Array.Empty<string>(),
        tags = Array.Empty<string>(),
    };

    private sealed record SubmitResult(Guid Id, string Key);
    private sealed record MemberRow(Guid PublicId, string Role);

    // Shared setup: submit a topic as a Member, then accept it as Secretary (Submitted → Accepted).
    // AcceptTopicHandler calls BeginTriage() then Accept() in one shot (W2 + W3 rollup).
    // Secretary role satisfies Policies.TopicTriage directly (no ABAC needed).
    private static async Task<(Guid TopicId, string Key, AcmpWebApplicationFactory Factory)>
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

        return (topic.Id, topic.Key, factory);
    }

    // ---- DeferTopicBody ----------------------------------------------------------------

    [Fact] // FluentValidation: Reason.NotEmpty → 400; covers DeferTopicBody binding
    public async Task Defer_empty_reason_returns_400()
    {
        var (topicId, _, factory) = await CreateAcceptedTopicAsync();
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
        var (topicId, _, factory) = await CreateAcceptedTopicAsync();
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
        var (topicId, _, factory) = await CreateAcceptedTopicAsync();
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

    [Fact] // AC-043: MoveBody happy path — Secretary reorders by a ±1 delta → 204 (single-item column = no-op)
    public async Task Secretary_moves_topic_priority_returns_204()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var topic = await (await Client(factory, "Member", sub: "kc-omar")
            .PostAsJsonAsync("/api/topics", SubmitBody()))
            .Content.ReadFromJsonAsync<SubmitResult>();

        var response = await Client(factory, "Secretary")
            .PostAsJsonAsync($"/api/topics/{topic!.Id}/priority/move", new { delta = 1 });

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
                streams = new[] { "core" },
                systems = Array.Empty<string>(),
                tags = Array.Empty<string>(),
            });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // DEF-059 half one, over HTTP: the empty list is refused at the boundary with a 400 naming the
    // field, rather than reaching the aggregate and surfacing as a 409 nobody can act on.
    [Fact]
    public async Task Emptying_a_topics_streams_returns_400()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var topic = await (await Client(factory, "Member", sub: "kc-submitter")
            .PostAsJsonAsync("/api/topics", SubmitBody()))
            .Content.ReadFromJsonAsync<SubmitResult>();

        var response = await Client(factory, "Member", sub: "kc-submitter").PutAsJsonAsync(
            $"/api/topics/{topic!.Id}", UpdateBody(streams: Array.Empty<string>()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // DEF-058 over HTTP, both halves of the gate in one pair. Elevating scope widens write access to
    // every stream-bounded member (ADR-0043 clause 5), so it is a triage act wherever it happens —
    // including on the pre-Accept path, where a submitter editing their OWN topic otherwise passes
    // no authorization check at all.
    [Fact]
    public async Task The_submitter_cannot_elevate_their_own_topics_scope_but_the_secretary_can()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var topic = await (await Client(factory, "Member", sub: "kc-submitter")
            .PostAsJsonAsync("/api/topics", SubmitBody()))
            .Content.ReadFromJsonAsync<SubmitResult>();

        var asSubmitter = await Client(factory, "Member", sub: "kc-submitter").PutAsJsonAsync(
            $"/api/topics/{topic!.Id}", UpdateBody(scope: "OrgWide"));
        asSubmitter.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var asSecretary = await Client(factory, "Secretary", sub: "kc-sec").PutAsJsonAsync(
            $"/api/topics/{topic.Id}", UpdateBody(scope: "OrgWide"));
        asSecretary.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Read it back: a 204 proves the request was accepted, not that the value landed. Platform
        // and OrgWide were unreachable enum values before this endpoint carried Scope (DEF-058).
        var detail = await Client(factory, "Member", sub: "kc-submitter")
            .GetFromJsonAsync<JsonElement>($"/api/topics/{topic.Key}");   // detail is keyed by TOP-YYYY-###
        detail.GetProperty("scope").GetString().Should().Be("OrgWide");
    }

    // AC-034 LITERALLY, over HTTP: an ACCEPTED topic, a Member who is NOT its Owner, an attempt to
    // edit the description → 403. Every prior piece of evidence for this AC was handler-level
    // (PermissionMatrixTests / TopicHandlerTests), which is a different claim: those construct the
    // authorization decision directly, while this one travels the real pipeline the AC describes.
    // ⚠ THE ACTOR IS A NON-OWNER MEMBER, not an unassigned one and not the submitter — a refusal of
    // either would prove something else. kc-owner is the Owner; kc-other is a Member with no
    // relationship to this topic at all.
    [Fact]
    public async Task A_member_who_is_not_the_owner_is_refused_editing_an_accepted_topic()
    {
        var (topicId, _, factory) = await CreateAcceptedTopicAsync();
        await using var _ = factory;

        var response = await Client(factory, "Member", sub: "kc-other").PutAsJsonAsync(
            $"/api/topics/{topicId}", UpdateBody(title: "Rewritten after the fact"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // The other half of the same AC clause: the Secretary MAY edit metadata after Acceptance, while
    // the content stays locked. Asserted by reading the topic back — a 204 says the request was
    // accepted, not that the aggregate honoured the lock.
    [Fact]
    public async Task After_acceptance_the_secretary_edits_metadata_and_the_content_stays_locked()
    {
        var (topicId, key, factory) = await CreateAcceptedTopicAsync();
        await using var _ = factory;
        var before = await Client(factory, "Secretary", sub: "kc-sec")
            .GetFromJsonAsync<JsonElement>($"/api/topics/{key}");
        var originalTitle = before.GetProperty("title").GetString();

        var response = await Client(factory, "Secretary", sub: "kc-sec").PutAsJsonAsync(
            $"/api/topics/{topicId}", UpdateBody(title: "Rewritten after the fact", streams: new[] { "government" }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var after = await Client(factory, "Secretary", sub: "kc-sec")
            .GetFromJsonAsync<JsonElement>($"/api/topics/{key}");
        after.GetProperty("title").GetString().Should().Be(originalTitle, "content is locked against retroactive modification");
        after.GetProperty("streams").EnumerateArray().Select(s => s.GetString())
            .Should().BeEquivalentTo(new[] { "government" }, "metadata stays editable after Acceptance");
    }

    private static object UpdateBody(string[]? streams = null, string? scope = null, string title = "Adopt Keycloak") => new
    {
        title,
        description = "Updated description.",
        justification = "Clearer justification.",
        urgency = "Normal",
        streams = streams ?? new[] { "core" },
        systems = Array.Empty<string>(),
        tags = Array.Empty<string>(),
        scope,
    };
}
