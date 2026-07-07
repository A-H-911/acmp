namespace Acmp.Modules.Integrations.Webex;

// Webex answered HTTP 429. Carries the server's Retry-After so the send job reschedules for exactly that
// delay instead of spin-waiting (webex-feasibility.md §2.2; never retry in a tight loop).
public sealed class WebexRateLimitException(TimeSpan retryAfter)
    : Exception($"Webex rate-limited the request; retry after {retryAfter.TotalSeconds:0}s.")
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}

// Any other non-success Webex response. Lets the Hangfire job bubble so AutomaticRetry backs off and,
// after the cap, dead-letters. The body is truncated and never contains our bot token.
public sealed class WebexApiException(int statusCode, string body)
    : Exception($"Webex API returned HTTP {statusCode}: {body}")
{
    public int StatusCode { get; } = statusCode;
}
