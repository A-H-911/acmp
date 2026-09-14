using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Acmp.Api.Tests;

// AC-169 / DEF-180: GET /api/uploads/limits returns the SAME options the validators enforce, so the limit a page
// states is the server's. The override case proves it reads configuration, not a copy of the defaults.
public class UploadLimitsApiTests : IClassFixture<AcmpWebApplicationFactory>
{
    private readonly AcmpWebApplicationFactory _factory;

    public UploadLimitsApiTests(AcmpWebApplicationFactory factory) => _factory = factory.Reset();

    private sealed record Limits(long AttachmentMaxBytes, string[] AttachmentContentTypes, long RecordingMaxBytes, string[] RecordingContentTypes);

    [Fact]
    public async Task The_defaults_are_the_validators_defaults()
    {
        var limits = await TopicAttachmentHttp.Client(_factory, "Member", "kc-m").GetFromJsonAsync<Limits>("/api/uploads/limits");

        limits!.AttachmentMaxBytes.Should().Be(100L * 1024 * 1024);
        limits.RecordingMaxBytes.Should().Be(2L * 1024 * 1024 * 1024);
        limits.AttachmentContentTypes.Should().Contain("application/pdf");
        limits.RecordingContentTypes.Should().BeEquivalentTo("video/mp4", "video/webm", "video/quicktime");
    }

    // Parity, not just plumbing: the number the endpoint states is the number the upload is refused at.
    [Fact]
    public async Task A_configured_maximum_is_what_the_endpoint_returns_and_what_an_upload_is_refused_at()
    {
        var app = TopicAttachmentHttp.WithFakeStore(_factory.WithWebHostBuilder(b => b.UseSetting("Topics:Attachments:MaxSizeBytes", "12345")));
        var member = TopicAttachmentHttp.Client(app, "Member", "kc-m");

        var stated = (await member.GetFromJsonAsync<Limits>("/api/uploads/limits"))!.AttachmentMaxBytes;
        var topic = await TopicAttachmentHttp.SubmitAsync(member);

        stated.Should().Be(12345);
        (await member.PostAsync($"/api/topics/{topic.Id}/attachments", TopicAttachmentHttp.Pdf(stated))).StatusCode.Should().Be(HttpStatusCode.Created);
        var over = await member.PostAsync($"/api/topics/{topic.Id}/attachments", TopicAttachmentHttp.Pdf(stated + 1));
        over.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await over.Content.ReadAsStringAsync()).Should().Contain("FILE_TOO_LARGE");
    }

    [Fact] // AC-008
    public async Task It_requires_a_signed_in_user()
    {
        (await _factory.CreateClient().GetAsync("/api/uploads/limits")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
