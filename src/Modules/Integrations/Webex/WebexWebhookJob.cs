using Acmp.Shared.Contracts.Meetings;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Acmp.Modules.Integrations.Webex;

// Processes a verified recording-ready webhook (executed on the worker container). Fetches the recording
// reference and attaches it to the correlated ACMP meeting via the write seam. Idempotent (the attach is a
// deterministic set), so a re-delivered webhook is harmless. Uncorrelated recordings are logged and dropped.
public sealed class WebexWebhookJob
{
    private readonly IWebexApiClient _client;
    private readonly IMeetingWebexWriter _writer;
    private readonly ILogger<WebexWebhookJob> _logger;

    public WebexWebhookJob(IWebexApiClient client, IMeetingWebexWriter writer, ILogger<WebexWebhookJob> logger)
    {
        _client = client;
        _writer = writer;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task ProcessRecordingAsync(string recordingId, CancellationToken ct)
    {
        var recording = await _client.GetRecordingAsync(recordingId, ct);
        if (recording is null)
        {
            _logger.LogWarning("Webex recording {RecordingId} was not found", recordingId);
            return;
        }

        if (string.IsNullOrWhiteSpace(recording.MeetingId))
        {
            _logger.LogWarning("Webex recording {RecordingId} carries no meeting id — cannot correlate", recordingId);
            return;
        }

        var attached = await _writer.AttachRecordingAsync(
            recording.MeetingId,
            new RecordingReference(recording.PlaybackUrl, recording.DownloadUrl, recording.DurationSeconds),
            ct);

        if (!attached)
            _logger.LogInformation(
                "Webex recording {RecordingId} (meeting {MeetingId}) matched no ACMP meeting", recordingId, recording.MeetingId);
    }
}
