using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Traceability.Domain.Enums;
using Acmp.Modules.Traceability.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Acmp.Api.Tests;

// HTTP-contract tests for /api/topics through the real pipeline + policy authorization + ABAC.
public class TopicApiTests
{
    private static HttpClient Client(AcmpWebApplicationFactory factory, string? roles, string sub = "u1") =>
        Client((WebApplicationFactory<Program>)factory, roles, sub);

    private static HttpClient Client(WebApplicationFactory<Program> app, string? roles, string sub = "u1")
    {
        var client = app.CreateClient();
        if (roles is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
            client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        }
        return client;
    }

    // Stand-in for the MinIO-backed store so the attachment endpoint runs without a live object store.
    private sealed class FakeFileStore : IFileStore
    {
        public Task<string> UploadAsync(string bucket, string objectName, Stream content, string contentType, CancellationToken ct = default)
            => Task.FromResult($"{bucket}/{objectName}");
        public Task<string> GetPreSignedUrlAsync(string bucket, string objectName, TimeSpan expiry, CancellationToken ct = default)
            => Task.FromResult($"https://minio.test/{bucket}/{objectName}");
        public Task<bool> ExistsAsync(string bucket, string objectName, CancellationToken ct = default) => Task.FromResult(true);
        public Task DeleteAsync(string bucket, string objectName, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static WebApplicationFactory<Program> WithFakeStore(AcmpWebApplicationFactory factory) =>
        factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IFileStore>();
            s.AddSingleton<IFileStore>(new FakeFileStore());
        }));

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
    // FR-030 needs the TYPE as well; a separate shape rather than widening TopicRow, which several
    // other tests deserialize and which has no reason to grow a field only one of them reads.
    private sealed record ConvertedRow(string Key, string Status, string Type);
    private sealed record Backlog(List<TopicRow> Items, int Total);
    private sealed record MemberRow(Guid PublicId, string Role);

    [Fact] // AC-008
    public async Task Submit_without_token_returns_401()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, roles: null).PostAsJsonAsync("/api/topics", SubmitBody("core"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // AC-005/006: Auditor is not in Topic.Submit
    public async Task Auditor_cannot_submit_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, "Auditor").PostAsJsonAsync("/api/topics", SubmitBody("core"));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // AC-030
    public async Task Submit_without_a_stream_returns_400()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, "Member").PostAsJsonAsync("/api/topics", SubmitBody());
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ADR-0042 clause (7) THROUGH THE REAL HOST. The unit tests prove the RULE with a fake catalog;
    // this proves the WIRING — that IStreamCatalog actually resolves from the composed container and
    // that the validator runs in the real MediatR pipeline. Those are different claims, and this
    // session's whole theme is that a correct-but-unwired control passes every check except this one.
    // "Platform" is the exact value every fixture carried before the taxonomy existed.
    [Fact]
    public async Task Submit_with_a_stream_outside_the_taxonomy_returns_400()
    {
        await using var factory = new AcmpWebApplicationFactory();

        var response = await Client(factory, "Member").PostAsJsonAsync("/api/topics", SubmitBody("Platform"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ⚠ The wildcard is member-side only (ADR-0042 clause 4) — a topic may never claim it. Asserted
    // over HTTP as well as in the unit tests because the exclusion lives in StreamCatalog, which the
    // unit tests replace with a fake: only this path exercises the real filter.
    [Fact]
    public async Task Submit_with_the_wildcard_stream_returns_400()
    {
        await using var factory = new AcmpWebApplicationFactory();

        var response = await Client(factory, "Member").PostAsJsonAsync("/api/topics", SubmitBody("all-streams"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact] // W1 + backlog + detail round-trip over HTTP
    public async Task Submit_then_read_backlog_and_detail()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var member = Client(factory, "Member", sub: "kc-omar");

        var submit = await member.PostAsJsonAsync("/api/topics", SubmitBody("core", "government"));
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

        var submit = await Client(factory, "Member", sub: "kc-omar").PostAsJsonAsync("/api/topics", SubmitBody("core"));
        var topic = await submit.Content.ReadFromJsonAsync<SubmitResult>();

        var owner = (await (await Client(factory, "Secretary").GetAsync("/api/members"))
            .Content.ReadFromJsonAsync<List<MemberRow>>())!.Single(m => m.Role == nameof(CommitteeRole.Member));
        var body = new { ownerId = owner.PublicId, ownerName = "Owner One" };

        var asMember = await Client(factory, "Member").PostAsJsonAsync($"/api/topics/{topic!.Id}/accept", body);
        asMember.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var asSecretary = await Client(factory, "Secretary", sub: "kc-sec").PostAsJsonAsync($"/api/topics/{topic.Id}/accept", body);
        asSecretary.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact] // W4 (AC-035): accepted → prepared over HTTP. Exercises the real pipeline so the prepare
    // handler's ICommitteeDirectory + INotificationChannel dependencies must actually resolve in DI —
    // a mocked unit test can't prove that. The Secretary roster fan-out is asserted at the handler level.
    public async Task Secretary_prepares_an_accepted_topic_returns_204()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedMembersAsync(("kc-owner", "Owner One", CommitteeRole.Member));

        var submit = await Client(factory, "Member", sub: "kc-omar").PostAsJsonAsync("/api/topics", SubmitBody("core"));
        var topic = await submit.Content.ReadFromJsonAsync<SubmitResult>();

        var owner = (await (await Client(factory, "Secretary").GetAsync("/api/members"))
            .Content.ReadFromJsonAsync<List<MemberRow>>())!.Single(m => m.Role == nameof(CommitteeRole.Member));
        var sec = Client(factory, "Secretary", sub: "kc-sec");

        (await sec.PostAsJsonAsync($"/api/topics/{topic!.Id}/accept", new { ownerId = owner.PublicId, ownerName = "Owner One" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var prepared = await sec.PostAsync($"/api/topics/{topic.Id}/prepare", null);
        prepared.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await sec.GetAsync($"/api/topics/{topic.Key}");
        (await detail.Content.ReadFromJsonAsync<TopicRow>())!.Status.Should().Be("Prepared");
    }

    // FR-045 / AC-112 — reopen over HTTP. Rejected is reachable straight from Submitted, so this
    // exercises the real route, policy and handler end to end.
    //
    // ⚠ The CLOSED branch of Reopen is NOT reachable from here, and neither is POST /close: both need
    // a topic at Decided, and TopicDecisionRecorder.MarkDecidedAsync SILENTLY RETURNS unless the topic
    // is InCommittee, which requires the whole meetings flow (schedule → publish agenda → conduct).
    // Both are covered at the handler level instead — including the Closed→Reopened branch — and the
    // gap is named here rather than left for someone to discover.
    [Fact]
    public async Task Secretary_reopens_a_rejected_topic_returns_204()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var submit = await Client(factory, "Member", sub: "kc-omar").PostAsJsonAsync("/api/topics", SubmitBody("core"));
        var topic = await submit.Content.ReadFromJsonAsync<SubmitResult>();
        var sec = Client(factory, "Secretary", sub: "kc-sec");

        (await sec.PostAsJsonAsync($"/api/topics/{topic!.Id}/reject", new { reason = "out of committee scope" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reopened = await sec.PostAsJsonAsync($"/api/topics/{topic.Id}/reopen",
            new { reason = "new regulatory guidance changes the assessment" });
        reopened.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await sec.GetAsync($"/api/topics/{topic.Key}");
        (await detail.Content.ReadFromJsonAsync<TopicRow>())!.Status.Should().Be("Reopened");
    }

    // FR-161 / AC-110 — a deferred topic comes back. Accept auto-triages, and Defer accepts an
    // Accepted topic, so the whole round trip is reachable over HTTP.
    [Fact]
    public async Task Secretary_returns_a_deferred_topic_to_triage_returns_204()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedMembersAsync(("kc-owner", "Owner One", CommitteeRole.Member));

        var submit = await Client(factory, "Member", sub: "kc-omar").PostAsJsonAsync("/api/topics", SubmitBody("core"));
        var topic = await submit.Content.ReadFromJsonAsync<SubmitResult>();
        var owner = (await (await Client(factory, "Secretary").GetAsync("/api/members"))
            .Content.ReadFromJsonAsync<List<MemberRow>>())!.Single(m => m.Role == nameof(CommitteeRole.Member));
        var sec = Client(factory, "Secretary", sub: "kc-sec");

        (await sec.PostAsJsonAsync($"/api/topics/{topic!.Id}/accept", new { ownerId = owner.PublicId, ownerName = "Owner One" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await sec.PostAsJsonAsync($"/api/topics/{topic.Id}/defer",
            new { reason = "awaiting vendor confirmation", revisitOn = (DateTimeOffset?)null }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reactivated = await sec.PostAsync($"/api/topics/{topic.Id}/reactivate", null);
        reactivated.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await sec.GetAsync($"/api/topics/{topic.Key}");
        (await detail.Content.ReadFromJsonAsync<TopicRow>())!.Status.Should().Be("Triage");
    }

    // FR-160 / AC-109 — close over HTTP. The topic is seeded already Decided because that status is
    // not reachable through the API (see SeedDecidedTopicAsync); everything from the route down is
    // the real pipeline.
    [Fact]
    public async Task Secretary_closes_a_decided_topic_returns_204()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var topicId = await factory.SeedDecidedTopicAsync();
        var sec = Client(factory, "Secretary", sub: "kc-sec");

        var closed = await sec.PostAsync($"/api/topics/{topicId}/close", null);

        closed.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // FR-030 / AC-113 — convert over HTTP. Same seeding reason as close: Decided is not reachable
    // through the API. Unlike the other lifecycle transitions this returns 201 with the SUCCESSOR's
    // key, because the artifact the caller should look at next is the new topic, not the retired one.
    [Fact]
    public async Task Secretary_converts_a_decided_topic_returns_201_with_the_successor()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var topicId = await factory.SeedDecidedTopicAsync();
        var sec = Client(factory, "Secretary", sub: "kc-sec");

        var response = await sec.PostAsJsonAsync($"/api/topics/{topicId}/convert",
            new { targetType = "EnhancementInnovation", reason = "research concluded" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<SubmitResult>();
        created!.Key.Should().NotBeNullOrWhiteSpace();

        // The successor is a real, readable topic of the requested type — not just an id in a body.
        var detail = await sec.GetAsync($"/api/topics/{created.Key}");
        var row = await detail.Content.ReadFromJsonAsync<ConvertedRow>();
        row!.Type.Should().Be("EnhancementInnovation");
        row.Status.Should().Be("Submitted", "the successor re-enters the pipeline for triage");

        // ⚠ THE TYPED LINK, ASSERTED AS A ROW IN THE REAL TRACEABILITY STORE. Every other test of this
        // feature substitutes ITraceabilityWriter, so they prove the handler CALLS the port and nothing
        // more — the edge FR-030 actually demands could be absent and they would all still pass. This
        // reads the Traceability module's own DbContext (the factory gives it a SEPARATE database from
        // Topics, so it must be resolved, never assumed to share Topics' store).
        using var scope = factory.Services.CreateScope();
        var trace = scope.ServiceProvider.GetRequiredService<TraceabilityDbContext>();
        var edge = await trace.Relationships.SingleAsync(r => r.TargetId == created.Id);
        edge.RelType.Should().Be(RelationshipType.ConvertedTo);
        edge.SourceType.Should().Be(ArtifactType.Topic);
        edge.TargetType.Should().Be(ArtifactType.Topic);
        edge.SourceId.Should().Be(topicId, "the edge runs original -> successor; reversed, both panels would name the successor as the origin");
        edge.IsActive.Should().BeTrue();
    }

    [Fact] // FR-030: the reason is mandatory, refused at the boundary rather than by the aggregate
    public async Task Convert_without_a_reason_returns_400()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var topicId = await factory.SeedDecidedTopicAsync();

        var response = await Client(factory, "Secretary", sub: "kc-sec")
            .PostAsJsonAsync($"/api/topics/{topicId}/convert",
                new { targetType = "EnhancementInnovation", reason = "" });

        // 400, not 500: the validator catches it before Topic.RequireReason throws.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact] // AC-031
    public async Task Reject_without_a_reason_returns_400()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var submit = await Client(factory, "Member", sub: "kc-omar").PostAsJsonAsync("/api/topics", SubmitBody("core"));
        var topic = await submit.Content.ReadFromJsonAsync<SubmitResult>();

        var response = await Client(factory, "Secretary").PostAsJsonAsync($"/api/topics/{topic!.Id}/reject", new { reason = "" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact] // BL-033: comment by any authenticated member
    public async Task Member_can_comment_on_a_topic()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var member = Client(factory, "Member", sub: "kc-omar");
        var submit = await member.PostAsJsonAsync("/api/topics", SubmitBody("core"));
        var topic = await submit.Content.ReadFromJsonAsync<SubmitResult>();

        var response = await member.PostAsJsonAsync($"/api/topics/{topic!.Id}/comments", new { reason = "Agreed; document rollback." });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact] // W20: Secretary rejects a submitted topic with a mandatory rationale -> 204
    public async Task Secretary_rejects_a_submitted_topic_returns_204()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var submit = await Client(factory, "Member", sub: "kc-omar").PostAsJsonAsync("/api/topics", SubmitBody("core"));
        var topic = await submit.Content.ReadFromJsonAsync<SubmitResult>();

        var response = await Client(factory, "Secretary", sub: "kc-sec")
            .PostAsJsonAsync($"/api/topics/{topic!.Id}/reject", new { reason = "Out of committee scope." });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact] // AC-049/050: the submitter attaches a PDF to their own topic (multipart) -> 201
    public async Task Submitter_attaches_a_file_to_their_topic_returns_201()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var app = WithFakeStore(factory);
        var member = Client(app, "Member", sub: "kc-omar");
        var submit = await member.PostAsJsonAsync("/api/topics", SubmitBody("core"));
        var topic = await submit.Content.ReadFromJsonAsync<SubmitResult>();

        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46 });
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "spec.pdf"); // field name must match the endpoint's IFormFile parameter ("file")

        var response = await member.PostAsync($"/api/topics/{topic!.Id}/attachments", form);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
