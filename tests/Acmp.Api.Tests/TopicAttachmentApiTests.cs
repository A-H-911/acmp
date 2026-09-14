using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Acmp.Modules.Topics.Application.Contracts;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Audit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Acmp.Api.Tests;

// Shared by the two classes below: a stand-in object store and the submit/attach calls they both make.
internal static class TopicAttachmentHttp
{
    public sealed class FakeFileStore : IFileStore
    {
        public Task<string> UploadAsync(string bucket, string objectName, Stream content, string contentType, CancellationToken ct = default)
            => Task.FromResult($"{bucket}/{objectName}");
        public Task<string> GetPreSignedUrlAsync(string bucket, string objectName, TimeSpan expiry, CancellationToken ct = default)
            => Task.FromResult($"https://minio.test/{bucket}/{objectName}");
        public Task<bool> ExistsAsync(string bucket, string objectName, CancellationToken ct = default) => Task.FromResult(true);
        public Task DeleteAsync(string bucket, string objectName, CancellationToken ct = default) => Task.CompletedTask;
    }

    public static WebApplicationFactory<Program> WithFakeStore(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IFileStore>();
            s.AddSingleton<IFileStore>(new FakeFileStore());
        }));

    public static HttpClient Client(WebApplicationFactory<Program> app, string roles, string sub)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        return client;
    }

    public sealed record SubmitResult(Guid Id, string Key);
    public sealed record AttachmentResult(Guid Id);

    public static async Task<SubmitResult> SubmitAsync(HttpClient submitter)
    {
        var response = await submitter.PostAsJsonAsync("/api/topics", new
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
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<SubmitResult>())!;
    }

    // A PDF of exactly `size` bytes: the magic number the content inspector checks, then zeros.
    public static MultipartFormDataContent Pdf(long size)
    {
        var bytes = new byte[size];
        "%PDF-1.7\n"u8.CopyTo(bytes);
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return new MultipartFormDataContent { { file, "file", "spec.pdf" } };
    }
}

// WBS-40.12 / AC-163 (DEC-190) over HTTP: who may open a topic attachment, the audit row, and the refusals.
public class TopicAttachmentDownloadApiTests : IClassFixture<AcmpWebApplicationFactory>
{
    private readonly AcmpWebApplicationFactory _factory;

    public TopicAttachmentDownloadApiTests(AcmpWebApplicationFactory factory) => _factory = factory.Reset();

    private sealed record UrlResult(string Url);

    private async Task<(WebApplicationFactory<Program> App, TopicAttachmentHttp.SubmitResult Topic, Guid AttachmentId)> TopicWithAttachmentAsync()
    {
        var app = TopicAttachmentHttp.WithFakeStore(_factory);
        var submitter = TopicAttachmentHttp.Client(app, "Member", "kc-submitter");
        var topic = await TopicAttachmentHttp.SubmitAsync(submitter);
        var attach = await submitter.PostAsync($"/api/topics/{topic.Id}/attachments", TopicAttachmentHttp.Pdf(64));
        attach.StatusCode.Should().Be(HttpStatusCode.Created);
        var attachment = await attach.Content.ReadFromJsonAsync<TopicAttachmentHttp.AttachmentResult>();
        return (app, topic, attachment!.Id);
    }

    private async Task<int> AccessRowsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        return await audit.AuditEvents.CountAsync(e => (e.Action ?? e.EventType) == "Topics.AttachmentAccessed");
    }

    [Fact]
    public async Task A_member_who_can_read_the_topic_opens_its_attachment_and_one_audit_row_is_written()
    {
        var (app, topic, attachmentId) = await TopicWithAttachmentAsync();
        var before = await AccessRowsAsync();

        var response = await TopicAttachmentHttp.Client(app, "Member", "kc-reader")
            .GetAsync($"/api/topics/{topic.Id}/attachments/{attachmentId}/url");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<UrlResult>())!.Url.Should().StartWith("https://minio.test/");
        (await AccessRowsAsync()).Should().Be(before + 1);
    }

    [Fact] // FR-163: a Restricted topic outside the caller's grants is NOT FOUND, and nothing is audited.
    public async Task A_member_without_a_grant_gets_404_on_a_restricted_topics_attachment()
    {
        var (app, topic, attachmentId) = await TopicWithAttachmentAsync();
        (await TopicAttachmentHttp.Client(app, "Secretary", "kc-sec")
            .PutAsJsonAsync($"/api/topics/{topic.Id}/confidentiality", new { restricted = true }))
            .IsSuccessStatusCode.Should().BeTrue();
        var before = await AccessRowsAsync();

        var response = await TopicAttachmentHttp.Client(app, "Member", "kc-reader")
            .GetAsync($"/api/topics/{topic.Id}/attachments/{attachmentId}/url");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AccessRowsAsync()).Should().Be(before);
    }

    [Fact] // The Secretary reads every topic, Restricted included, so the same attachment opens for them.
    public async Task The_secretary_opens_a_restricted_topics_attachment()
    {
        var (app, topic, attachmentId) = await TopicWithAttachmentAsync();
        var sec = TopicAttachmentHttp.Client(app, "Secretary", "kc-sec");
        (await sec.PutAsJsonAsync($"/api/topics/{topic.Id}/confidentiality", new { restricted = true })).IsSuccessStatusCode.Should().BeTrue();

        (await sec.GetAsync($"/api/topics/{topic.Id}/attachments/{attachmentId}/url")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_unknown_attachment_is_404()
    {
        var (app, topic, _) = await TopicWithAttachmentAsync();

        (await TopicAttachmentHttp.Client(app, "Member", "kc-reader").GetAsync($"/api/topics/{topic.Id}/attachments/{Guid.NewGuid()}/url"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact] // A guest-only principal is refused here as on every /api/topics route (GuestSurfaceMiddleware).
    public async Task A_guest_is_refused()
    {
        var (app, topic, attachmentId) = await TopicWithAttachmentAsync();

        var response = await TopicAttachmentHttp.Client(app, "Guest", "kc-guest").GetAsync($"/api/topics/{topic.Id}/attachments/{attachmentId}/url");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Headers.GetValues("X-Acmp-Auth-Reason").Should().ContainSingle().Which.Should().Be("guest_scope");
    }
}

// AC-162 (DEC-190 a3), DEF-154 + DEF-166: the upload limit on REAL KESTREL. TestServer enforces no request-body
// limit at all, which is how Kestrel's 30,000,000-byte default refused valid files for months with every test
// green; so this class hosts the API on Kestrel over a loopback socket. Its own host (not the shared fixture):
// the server kind is fixed when the host starts.
public sealed class TopicAttachmentKestrelLimitTests : IDisposable
{
    private const long Max = 100L * 1024 * 1024;
    private readonly WebApplicationFactory<Program> _app;

    public TopicAttachmentKestrelLimitTests()
    {
        _app = TopicAttachmentHttp.WithFakeStore(new AcmpWebApplicationFactory());
        _app.UseKestrel(0);
        _app.StartServer();
    }

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task A_100_MB_attachment_is_stored_and_one_just_over_the_maximum_gets_FILE_TOO_LARGE()
    {
        var submitter = TopicAttachmentHttp.Client(_app, "Member", "kc-submitter");
        submitter.Timeout = TimeSpan.FromMinutes(2);
        var topic = await TopicAttachmentHttp.SubmitAsync(submitter);

        var atMax = await submitter.PostAsync($"/api/topics/{topic.Id}/attachments", TopicAttachmentHttp.Pdf(Max));
        atMax.StatusCode.Should().Be(HttpStatusCode.Created, "a file at the configured maximum crosses Kestrel and is stored");

        // Over the maximum but inside the 1 MiB multipart margin: the server's own limit lets it through, so the
        // VALIDATOR answers, with the code the SPA translates - not a bare 413 before any handler runs.
        var over = await submitter.PostAsync($"/api/topics/{topic.Id}/attachments", TopicAttachmentHttp.Pdf(Max + 512 * 1024));
        over.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await over.Content.ReadAsStringAsync()).Should().Contain("FILE_TOO_LARGE");

        // ...and the refused file left nothing behind: the topic still lists only the one stored at the maximum.
        var detail = await submitter.GetFromJsonAsync<TopicDetailDto>($"/api/topics/{topic.Key}");
        detail!.Attachments.Should().ContainSingle().Which.SizeBytes.Should().Be(Max);
    }
}
