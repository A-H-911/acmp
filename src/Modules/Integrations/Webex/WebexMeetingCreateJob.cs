using Acmp.Modules.Integrations.Webex.Oauth;
using Acmp.Shared.Contracts.Meetings;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Acmp.Modules.Integrations.Webex;

// Creates the Webex meeting for a scheduled ACMP meeting (worker container) using the secretary OAuth token,
// then writes the Webex meeting id + join URL back via the seam so the recording webhook can later correlate.
// Degrades gracefully: if no OAuth token is available the meeting simply keeps its manually-entered details
// (AC-072) — the create is best-effort, never a scheduling blocker.
public sealed class WebexMeetingCreateJob
{
    private readonly IWebexTokenService _tokens;
    private readonly IWebexApiClient _client;
    private readonly IMeetingWebexWriter _writer;
    private readonly IWebexJobScheduler _scheduler;
    private readonly ILogger<WebexMeetingCreateJob> _logger;

    public WebexMeetingCreateJob(IWebexTokenService tokens, IWebexApiClient client,
        IMeetingWebexWriter writer, IWebexJobScheduler scheduler, ILogger<WebexMeetingCreateJob> logger)
    {
        _tokens = tokens;
        _client = client;
        _writer = writer;
        _scheduler = scheduler;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task CreateAsync(Guid meetingPublicId, string title, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        try
        {
            var token = await _tokens.GetValidAccessTokenAsync(ct);
            if (token is null)
            {
                _logger.LogWarning("No Webex OAuth token; skipping meeting auto-create for {MeetingId}", meetingPublicId);
                return;
            }

            var created = await _client.CreateMeetingAsync(token, title, start, end, ct);
            if (created is null)
            {
                _logger.LogWarning("Webex meeting create returned no meeting for {MeetingId}", meetingPublicId);
                return;
            }

            await _writer.SetWebexMeetingAsync(meetingPublicId, created.Id, created.JoinUrl, ct);
        }
        catch (WebexRateLimitException ex)
        {
            // 429 → reschedule this create for the server-supplied Retry-After (no tight loop), mirroring WebexSendJob.
            _logger.LogWarning("Webex rate-limited; rescheduling meeting auto-create for {MeetingId} in {Seconds}s",
                meetingPublicId, ex.RetryAfter.TotalSeconds);
            _scheduler.Schedule<WebexMeetingCreateJob>(
                j => j.CreateAsync(meetingPublicId, title, start, end, CancellationToken.None), ex.RetryAfter);
        }
    }
}
