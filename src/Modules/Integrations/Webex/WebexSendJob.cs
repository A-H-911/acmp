using Hangfire;
using Microsoft.Extensions.Logging;

namespace Acmp.Modules.Integrations.Webex;

// The Hangfire job (executed on the worker container) that posts a pre-built card to the committee space.
// 429 handling: on WebexRateLimitException it reschedules the SAME call for the server-supplied Retry-After
// and returns normally, so Hangfire does not also retry this attempt (no tight-loop). Any other failure
// bubbles → AutomaticRetry backs off, then dead-letters (visible in Admin → Job Monitor).
public sealed class WebexSendJob
{
    private readonly IWebexApiClient _client;
    private readonly IWebexJobScheduler _scheduler;
    private readonly ILogger<WebexSendJob> _logger;

    public WebexSendJob(IWebexApiClient client, IWebexJobScheduler scheduler, ILogger<WebexSendJob> logger)
    {
        _client = client;
        _scheduler = scheduler;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task SendSpaceMessageAsync(string messageRequestJson, CancellationToken ct)
    {
        try
        {
            await _client.PostSpaceMessageAsync(messageRequestJson, ct);
        }
        catch (WebexRateLimitException ex)
        {
            _logger.LogWarning("Webex rate-limited; rescheduling card send in {Seconds}s", ex.RetryAfter.TotalSeconds);
            _scheduler.Schedule<WebexSendJob>(j => j.SendSpaceMessageAsync(messageRequestJson, CancellationToken.None), ex.RetryAfter);
        }
    }
}
