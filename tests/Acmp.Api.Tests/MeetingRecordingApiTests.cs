using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Acmp.Modules.Meetings.Domain;
using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Acmp.Api.Tests;

// HTTP-contract tests for POST /api/meetings/{key}/recording (FR-056 manual upload) through the real pipeline
// + policy authorization (Minutes.Capture = Secretary/Chairman). IFileStore is faked — no live MinIO needed.
public class MeetingRecordingApiTests
{
    // Stand-in for the MinIO-backed store so the endpoint runs without a live object store.
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

    private static HttpClient Client(WebApplicationFactory<Program> app, string? roles, string sub = "kc-sec")
    {
        var client = app.CreateClient();
        if (roles is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
            client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        }
        return client;
    }

    private static MultipartFormDataContent VideoForm(string contentType = "video/mp4", string fileName = "meeting.mp4")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[] { 0x00, 0x01, 0x02, 0x03 });
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName); // field name must match the endpoint's IFormFile parameter ("file")
        return form;
    }

    private static async Task<string> SeedMeetingAsync(WebApplicationFactory<Program> app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        var m = Meeting.Schedule("MTG-2026-001", "Weekly Committee", Meeting.SingleCommitteeId, Guid.NewGuid(), "Chair",
            DateTimeOffset.Parse("2026-07-01T09:00:00Z"), DateTimeOffset.Parse("2026-07-01T10:30:00Z"),
            MeetingType.Regular, MeetingMode.Remote, null, null, DateTimeOffset.UtcNow);
        db.Meetings.Add(m);
        await db.SaveChangesAsync();
        return m.Key;
    }

    private sealed record RecordingJson(string Source, string? FileName, string? ContentType, long? SizeBytes);
    private sealed record MeetingRecordingSlice(RecordingJson? Recording);

    [Fact] // AC-008
    public async Task Upload_without_token_returns_401()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var resp = await Client(WithFakeStore(factory), roles: null)
            .PostAsync("/api/meetings/MTG-2026-001/recording", VideoForm());
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // Minutes.Capture is Secretary/Chairman — a Member is forbidden
    public async Task Member_cannot_upload_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var resp = await Client(WithFakeStore(factory), "Member")
            .PostAsync("/api/meetings/MTG-2026-001/recording", VideoForm());
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // disallowed MIME rejected by validation before the handler
    public async Task Disallowed_type_returns_400()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var resp = await Client(WithFakeStore(factory), "Secretary")
            .PostAsync("/api/meetings/MTG-2026-001/recording", VideoForm("application/x-msdownload", "x.exe"));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact] // unknown meeting → 404
    public async Task Unknown_meeting_returns_404()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var resp = await Client(WithFakeStore(factory), "Secretary")
            .PostAsync("/api/meetings/MTG-9999-999/recording", VideoForm());
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact] // Secretary uploads → 200; the meeting detail then surfaces the uploaded recording
    public async Task Secretary_uploads_and_detail_shows_recording()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var app = WithFakeStore(factory);
        var key = await SeedMeetingAsync(app);
        var sec = Client(app, "Secretary");

        var upload = await sec.PostAsync($"/api/meetings/{key}/recording", VideoForm("video/mp4", "board.mp4"));
        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        var rec = await upload.Content.ReadFromJsonAsync<RecordingJson>();
        rec!.Source.Should().Be("Uploaded");
        rec.FileName.Should().Be("board.mp4");

        var detail = await (await sec.GetAsync($"/api/meetings/{key}")).Content.ReadFromJsonAsync<MeetingRecordingSlice>();
        detail!.Recording!.Source.Should().Be("Uploaded");
        detail.Recording.FileName.Should().Be("board.mp4");
    }

    private sealed record UrlResponse(string Url);

    [Fact] // AC-008
    public async Task Recording_url_without_token_returns_401()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var resp = await Client(WithFakeStore(factory), roles: null).GetAsync("/api/meetings/MTG-2026-002/recording/url");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // no uploaded recording → 404
    public async Task Recording_url_404_when_no_recording()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var app = WithFakeStore(factory);
        var key = await SeedMeetingAsync(app);
        var resp = await Client(app, "Member").GetAsync($"/api/meetings/{key}/recording/url");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact] // any member can fetch the presigned playback URL after an upload
    public async Task Recording_url_200_after_upload()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var app = WithFakeStore(factory);
        var key = await SeedMeetingAsync(app);
        await Client(app, "Secretary").PostAsync($"/api/meetings/{key}/recording", VideoForm("video/mp4", "board.mp4"));

        var resp = await Client(app, "Member").GetAsync($"/api/meetings/{key}/recording/url");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadFromJsonAsync<UrlResponse>())!.Url.Should().StartWith("https://minio.test/");
    }

    [Fact] // AC-008
    public async Task Delete_without_token_returns_401()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var resp = await Client(WithFakeStore(factory), roles: null).DeleteAsync("/api/meetings/MTG-2026-002/recording");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // Minutes.Capture is Secretary/Chairman — a Member cannot delete
    public async Task Member_cannot_delete_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var app = WithFakeStore(factory);
        var key = await SeedMeetingAsync(app);
        var resp = await Client(app, "Member").DeleteAsync($"/api/meetings/{key}/recording");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // Secretary deletes → 204 and the meeting detail no longer carries a recording
    public async Task Secretary_deletes_recording_then_detail_is_null()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var app = WithFakeStore(factory);
        var key = await SeedMeetingAsync(app);
        var sec = Client(app, "Secretary");
        await sec.PostAsync($"/api/meetings/{key}/recording", VideoForm("video/mp4", "board.mp4"));

        (await sec.DeleteAsync($"/api/meetings/{key}/recording")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await (await sec.GetAsync($"/api/meetings/{key}")).Content.ReadFromJsonAsync<MeetingRecordingSlice>();
        detail!.Recording.Should().BeNull();
    }
}
