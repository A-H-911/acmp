using System.Linq.Expressions;
using Acmp.Modules.Integrations.Webex;
using Acmp.Modules.Integrations.Webex.Oauth;
using Acmp.Shared.Contracts.Meetings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Acmp.Application.Tests.Webex;

// The recording webhook processor: fetch the recording with the OAuth host token (recordings are user-scoped,
// the bot token 403s), correlate by its Webex meeting id, attach. Missing recordings, recordings with no meeting
// id, and the no-token case are skipped without touching the meeting store.
public class WebexWebhookJobTests
{
    private static WebexWebhookJob Job(IWebexApiClient client, IMeetingWebexWriter writer,
        IWebexTokenService? tokens = null, IWebexJobScheduler? scheduler = null) =>
        new(tokens ?? WithToken("user-token"), client, writer,
            scheduler ?? Substitute.For<IWebexJobScheduler>(), NullLogger<WebexWebhookJob>.Instance);

    private static IWebexTokenService WithToken(string? token)
    {
        var t = Substitute.For<IWebexTokenService>();
        t.GetValidAccessTokenAsync(Arg.Any<CancellationToken>()).Returns(token);
        return t;
    }

    [Fact]
    public async Task Fetches_the_recording_and_attaches_it_to_the_correlated_meeting()
    {
        var client = Substitute.For<IWebexApiClient>();
        client.GetRecordingAsync("user-token", "rec-1", Arg.Any<CancellationToken>())
            .Returns(new WebexRecording("rec-1", "webex-abc", "https://play", "https://dl", 1800));
        var writer = Substitute.For<IMeetingWebexWriter>();

        await Job(client, writer).ProcessRecordingAsync("rec-1", default);

        await writer.Received(1).AttachRecordingAsync("webex-abc",
            Arg.Is<RecordingReference>(r => r.PlaybackUrl == "https://play" && r.DurationSeconds == 1800),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Skips_when_the_recording_is_not_found()
    {
        var client = Substitute.For<IWebexApiClient>();
        client.GetRecordingAsync("user-token", "rec-x", Arg.Any<CancellationToken>()).Returns((WebexRecording?)null);
        var writer = Substitute.For<IMeetingWebexWriter>();

        await Job(client, writer).ProcessRecordingAsync("rec-x", default);

        await writer.DidNotReceive().AttachRecordingAsync(Arg.Any<string>(), Arg.Any<RecordingReference>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Skips_when_the_recording_has_no_meeting_id()
    {
        var client = Substitute.For<IWebexApiClient>();
        client.GetRecordingAsync("user-token", "rec-2", Arg.Any<CancellationToken>())
            .Returns(new WebexRecording("rec-2", null, "https://play", "https://dl", 60));
        var writer = Substitute.For<IMeetingWebexWriter>();

        await Job(client, writer).ProcessRecordingAsync("rec-2", default);

        await writer.DidNotReceive().AttachRecordingAsync(Arg.Any<string>(), Arg.Any<RecordingReference>(), Arg.Any<CancellationToken>());
    }

    [Fact] // No OAuth token (consent not completed) => never call Webex, never attach — best-effort, no throw.
    public async Task Skips_when_no_oauth_token_is_available()
    {
        var client = Substitute.For<IWebexApiClient>();
        var writer = Substitute.For<IMeetingWebexWriter>();

        await Job(client, writer, WithToken(null)).ProcessRecordingAsync("rec-3", default);

        await client.DidNotReceive().GetRecordingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await writer.DidNotReceive().AttachRecordingAsync(Arg.Any<string>(), Arg.Any<RecordingReference>(), Arg.Any<CancellationToken>());
    }

    [Fact] // m9: 429 reschedules the SAME fetch for the server Retry-After instead of tight-looping / dead-lettering.
    public async Task Reschedules_the_fetch_on_a_rate_limit()
    {
        var client = Substitute.For<IWebexApiClient>();
        client.GetRecordingAsync("user-token", "rec-9", Arg.Any<CancellationToken>())
            .ThrowsAsync(new WebexRateLimitException(TimeSpan.FromSeconds(30)));
        var writer = Substitute.For<IMeetingWebexWriter>();
        var scheduler = Substitute.For<IWebexJobScheduler>();

        await Job(client, writer, scheduler: scheduler).ProcessRecordingAsync("rec-9", default);

        scheduler.Received(1).Schedule<WebexWebhookJob>(
            Arg.Any<Expression<Func<WebexWebhookJob, Task>>>(), TimeSpan.FromSeconds(30));
        await writer.DidNotReceive().AttachRecordingAsync(Arg.Any<string>(), Arg.Any<RecordingReference>(), Arg.Any<CancellationToken>());
    }

    [Fact] // AC-069: a re-delivered/replayed webhook (same recording, within the window) attaches the SAME
           // deterministic reference — the outcome is idempotent, so the duplicate is harmless.
    public async Task Reprocessing_the_same_recording_is_idempotent()
    {
        var client = Substitute.For<IWebexApiClient>();
        client.GetRecordingAsync("user-token", "rec-1", Arg.Any<CancellationToken>())
            .Returns(new WebexRecording("rec-1", "webex-abc", "https://play", "https://dl", 1800));
        var writer = Substitute.For<IMeetingWebexWriter>();
        var job = Job(client, writer);

        await job.ProcessRecordingAsync("rec-1", default);
        await job.ProcessRecordingAsync("rec-1", default);

        // Both deliveries attach the identical reference — replaying converges on one end state (idempotent).
        await writer.Received(2).AttachRecordingAsync("webex-abc",
            Arg.Is<RecordingReference>(r => r.PlaybackUrl == "https://play" && r.DurationSeconds == 1800),
            Arg.Any<CancellationToken>());
    }
}
