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
    private readonly ILogger<WebexMeetingCreateJob> _logger;

    public WebexMeetingCreateJob(IWebexTokenService tokens, IWebexApiClient client,
        IMeetingWebexWriter writer, ILogger<WebexMeetingCreateJob> logger)
    {
        _tokens = tokens;
        _client = client;
        _writer = writer;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task CreateAsync(Guid meetingPublicId, string title, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
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
}
