using Acmp.Modules.Integrations.Webex.Oauth;
using Acmp.Shared.Contracts.Meetings;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Acmp.Modules.Integrations.Webex;

// Processes a verified recording-ready webhook (executed on the worker container). Fetches the recording
// reference and attaches it to the correlated ACMP meeting via the write seam. Idempotent (the attach is a
// deterministic set), so a re-delivered webhook is harmless. Uncorrelated recordings are logged and dropped.
public sealed class WebexWebhookJob
{
    private readonly IWebexTokenService _tokens;
    private readonly IWebexApiClient _client;
    private readonly IMeetingWebexWriter _writer;
    private readonly ILogger<WebexWebhookJob> _logger;

    public WebexWebhookJob(IWebexTokenService tokens, IWebexApiClient client, IMeetingWebexWriter writer,
        ILogger<WebexWebhookJob> logger)
    {
        _tokens = tokens;
        _client = client;
        _writer = writer;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task ProcessRecordingAsync(string recordingId, CancellationToken ct)
    {
        // Recordings are user-scoped — fetch with the secretary OAuth token (the bot token gets a 403). No token
        // (consent not completed) => drop the recording; the webhook is best-effort, never blocks anything.
        var token = await _tokens.GetValidAccessTokenAsync(ct);
        if (token is null)
        {
            _logger.LogWarning("No Webex OAuth token; cannot fetch recording {RecordingId}", recordingId);
            return;
        }

        var recording = await _client.GetRecordingAsync(token, recordingId, ct);
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
