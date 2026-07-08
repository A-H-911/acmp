using Acmp.Modules.Integrations.Webex;
using FluentAssertions;

namespace Acmp.Application.Tests.Webex;

// The recording reference DTO simply carries the correlation key and the reference URLs.
public class WebexRecordingTests
{
    [Fact]
    public void Carries_the_correlation_key_and_reference_urls()
    {
        var recording = new WebexRecording("rec-1", "webex-9", "https://playback", "https://download", 3600);

        recording.Id.Should().Be("rec-1");
        recording.MeetingId.Should().Be("webex-9");
        recording.PlaybackUrl.Should().Be("https://playback");
        recording.DownloadUrl.Should().Be("https://download");
        recording.DurationSeconds.Should().Be(3600);
    }
}
