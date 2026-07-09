using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
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

    [Fact] // W4 (AC-035): accepted → prepared over HTTP. Exercises the real pipeline so the prepare
    // handler's ICommitteeDirectory + INotificationChannel dependencies must actually resolve in DI —
    // a mocked unit test can't prove that. The Secretary roster fan-out is asserted at the handler level.
    public async Task Secretary_prepares_an_accepted_topic_returns_204()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedMembersAsync(("kc-owner", "Owner One", CommitteeRole.Member));

        var submit = await Client(factory, "Member", sub: "kc-omar").PostAsJsonAsync("/api/topics", SubmitBody("identity"));
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

    [Fact] // W20: Secretary rejects a submitted topic with a mandatory rationale -> 204
    public async Task Secretary_rejects_a_submitted_topic_returns_204()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var submit = await Client(factory, "Member", sub: "kc-omar").PostAsJsonAsync("/api/topics", SubmitBody("identity"));
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
        var submit = await member.PostAsJsonAsync("/api/topics", SubmitBody("identity"));
        var topic = await submit.Content.ReadFromJsonAsync<SubmitResult>();

        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46 });
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "spec.pdf"); // field name must match the endpoint's IFormFile parameter ("file")

        var response = await member.PostAsync($"/api/topics/{topic!.Id}/attachments", form);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
