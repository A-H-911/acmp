using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using Acmp.Modules.Topics.Application.Contracts;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Audit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;

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
        // AC-164/165: the download link names the file, so a test can tell it from the inline one.
        public Task<string> GetDownloadUrlAsync(string bucket, string objectName, string downloadFileName, TimeSpan expiry, CancellationToken ct = default)
            => Task.FromResult($"https://minio.test/{bucket}/{objectName}?download={Uri.EscapeDataString(downloadFileName)}");
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
        // AC-164: a DOWNLOAD link that saves under the original name (the fake store names it in the URL).
        (await response.Content.ReadFromJsonAsync<UrlResult>())!.Url.Should().StartWith("https://minio.test/").And.EndWith("?download=spec.pdf");
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
// ONE Kestrel-hosted class, run ALONE (measured, both): this host binds a fixed port, so a second Kestrel class in
// parallel fails to start. A non-parallel collection runs after every parallel one. (Running alone was ALSO meant to
// keep the static Serilog logger pointed at this host; it did not always - see the constructor, which no longer
// relies on it.)
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class KestrelHostCollection
{
    public const string Name = "Kestrel host, run alone";
}

[Collection(KestrelHostCollection.Name)]
public sealed class TopicAttachmentKestrelLimitTests : IDisposable
{
    private sealed class Capture : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();
        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    }

    private const long Max = 100L * 1024 * 1024;
    private readonly Capture _sink = new();
    private readonly WebApplicationFactory<Program> _app;

    // The capture must not depend on the PROCESS-WIDE static logger. UseSerilog's ILoggerFactory writes through
    // Log.Logger, and ANY other host built or disposed in this process re-points it (a disposal calls
    // Log.CloseAndFlush, which leaves a silent logger): under the full solution run the DEF-169 test once saw only
    // this host's start-up lines and none of its own requests. Binding the factory to THIS host's logger keeps the
    // shipped configuration - appsettings.json's RequestDelegateFactory Debug override lives in that logger -
    // and removes the shared state instead of lowering the odds of hitting it.
    public TopicAttachmentKestrelLimitTests()
    {
        _app = TopicAttachmentHttp.WithFakeStore(new AcmpWebApplicationFactory())
            .WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            {
                s.AddSingleton<ILogEventSink>(_sink);
                s.AddSingleton<ILoggerFactory>(sp => new SerilogLoggerFactory(sp.GetRequiredService<Serilog.ILogger>()));
            }));
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

    // DEF-169: on uat every large upload ended in a 400 before the handler ran, and the reason was logged below the
    // level production keeps, so nothing said why. appsettings.json now keeps Microsoft.AspNetCore.Http.
    // RequestDelegateFactory at Debug; this proves, with the SHIPPED configuration (no override here), that an
    // upload whose body ends early leaves its reason in the log. Real Kestrel: TestServer cannot cut a body short.
    [Fact]
    public async Task An_upload_whose_body_ends_early_logs_why_it_was_refused()
    {
        var client = TopicAttachmentHttp.Client(_app, "Member", "kc-submitter");
        var topic = await TopicAttachmentHttp.SubmitAsync(client);

        // Declare 4 MB, send 1 MB, let the server start reading, hang up - what a reloaded page does to an upload
        // in flight. (Hanging up before the server reads at all is a different failure: a 500, not uat's 400.)
        const string boundary = "----def169";
        var head = Encoding.ASCII.GetBytes($"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"a.pdf\"\r\nContent-Type: application/pdf\r\n\r\n%PDF-1.7\n");
        using (var tcp = new TcpClient())
        {
            await tcp.ConnectAsync("127.0.0.1", client.BaseAddress!.Port);
            var stream = tcp.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes(
                $"POST /api/topics/{topic.Id}/attachments HTTP/1.1\r\nHost: localhost\r\n" +
                $"{TestAuthHandler.RolesHeader}: Member\r\n{TestAuthHandler.SubHeader}: kc-submitter\r\n" +
                $"Content-Type: multipart/form-data; boundary={boundary}\r\nContent-Length: {4 * 1024 * 1024}\r\n\r\n"));
            await stream.WriteAsync(head);
            await stream.WriteAsync(new byte[1024 * 1024]);
            await Task.Delay(300);
        }

        LogEvent? reason = null;
        for (var i = 0; i < 50 && reason is null; i++)
        {
            reason = _sink.Events.FirstOrDefault(e =>
                e.Properties.TryGetValue("SourceContext", out var s) && s.ToString().Contains("RequestDelegateFactory"));
            if (reason is null) await Task.Delay(100);
        }

        reason.Should().NotBeNull("the refusal's reason must reach the sinks production keeps; captured: " + string.Join(" | ", _sink.Events.Select(e => $"{e.Level} {(e.Properties.TryGetValue("SourceContext", out var c) ? c : null)} {e.RenderMessage()}").TakeLast(12)));
        (reason!.Exception?.Message ?? reason.RenderMessage()).Should().Contain("Unexpected end of request content");
    }
}
