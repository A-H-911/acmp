using Acmp.Modules.Integrations.Webex;
using Acmp.Shared.Contracts.Meetings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Acmp.Application.Tests.Webex;

// The recording webhook processor: fetch the recording, correlate by its Webex meeting id, attach. Missing
// recordings and recordings with no meeting id are skipped without touching the meeting store.
public class WebexWebhookJobTests
{
    private static WebexWebhookJob Job(IWebexApiClient client, IMeetingWebexWriter writer) =>
        new(client, writer, NullLogger<WebexWebhookJob>.Instance);

    [Fact]
    public async Task Fetches_the_recording_and_attaches_it_to_the_correlated_meeting()
    {
        var client = Substitute.For<IWebexApiClient>();
        client.GetRecordingAsync("rec-1", Arg.Any<CancellationToken>())
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
        client.GetRecordingAsync("rec-x", Arg.Any<CancellationToken>()).Returns((WebexRecording?)null);
        var writer = Substitute.For<IMeetingWebexWriter>();

        await Job(client, writer).ProcessRecordingAsync("rec-x", default);

        await writer.DidNotReceive().AttachRecordingAsync(Arg.Any<string>(), Arg.Any<RecordingReference>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Skips_when_the_recording_has_no_meeting_id()
    {
        var client = Substitute.For<IWebexApiClient>();
        client.GetRecordingAsync("rec-2", Arg.Any<CancellationToken>())
            .Returns(new WebexRecording("rec-2", null, "https://play", "https://dl", 60));
        var writer = Substitute.For<IMeetingWebexWriter>();

        await Job(client, writer).ProcessRecordingAsync("rec-2", default);

        await writer.DidNotReceive().AttachRecordingAsync(Arg.Any<string>(), Arg.Any<RecordingReference>(), Arg.Any<CancellationToken>());
    }
}
