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
    private readonly IWebexJobScheduler _scheduler;
    private readonly ILogger<WebexWebhookJob> _logger;

    public WebexWebhookJob(IWebexTokenService tokens, IWebexApiClient client, IMeetingWebexWriter writer,
        IWebexJobScheduler scheduler, ILogger<WebexWebhookJob> logger)
    {
        _tokens = tokens;
        _client = client;
        _writer = writer;
        _scheduler = scheduler;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task ProcessRecordingAsync(string recordingId, CancellationToken ct)
    {
        try
        {
            // Recordings are user-scoped — fetch with the secretary OAuth token (the bot token gets a 403). No
            // token (consent not completed) => drop the recording; the webhook is best-effort, never blocks.
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
        catch (WebexRateLimitException ex)
        {
            // 429 → reschedule this SAME fetch for the server-supplied Retry-After (no tight loop), mirroring
            // WebexSendJob. Returning normally means Hangfire does not also retry this attempt.
            _logger.LogWarning("Webex rate-limited; rescheduling recording {RecordingId} fetch in {Seconds}s",
                recordingId, ex.RetryAfter.TotalSeconds);
            _scheduler.Schedule<WebexWebhookJob>(j => j.ProcessRecordingAsync(recordingId, CancellationToken.None), ex.RetryAfter);
        }
    }
}
