namespace Acmp.Modules.Integrations.Webex;

// The only code that speaks the Webex REST surface (Anti-Corruption Layer, integration-architecture.md §1.1).
// A seam so the sink/jobs are unit-tested against a fake and the real client is exercised via a mocked
// HttpMessageHandler. Throws WebexRateLimitException on 429, WebexApiException on any other non-success.
public interface IWebexApiClient
{
    // POST /messages — the body is a fully-formed request (roomId + adaptive-card attachment) from
    // AdaptiveCardBuilder. Kept as a pre-serialized string so the Hangfire job argument is trivially durable.
    Task PostSpaceMessageAsync(string messageRequestJson, CancellationToken ct = default);

    // GET /recordings/{id} — fetch the recording reference for the recording-ready webhook, authenticated with
    // the secretary OAuth USER token. Recordings are user-scoped (`meeting:recordings_read`); the bot token has
    // no meeting scopes and Webex answers 403 (confirmed live in the P13 sandbox). Returns null on 404.
    Task<WebexRecording?> GetRecordingAsync(string accessToken, string recordingId, CancellationToken ct = default);

    // POST /meetings — create a Webex meeting with the secretary OAuth USER token (a bot cannot host).
    // Returns the id + join URL, or null if the response carried no id.
    Task<CreatedWebexMeeting?> CreateMeetingAsync(string accessToken, string title,
        DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);

    // Ensure exactly one recordings/created webhook points at targetUrl (closes the AC-070 gap: without a
    // registered webhook nothing ever fires). GET /webhooks then reconcile — keep one hook already on the
    // current targetUrl (assumed ours; Webex redacts the secret on read so we cannot re-verify it), delete
    // the rest (stale ngrok URLs, duplicates from a concurrent registration), and POST-create when none
    // match. Uses the secretary OAuth USER token — recordings are host-owned, and webhook creation needs
    // read scope on the resource (meeting:recordings_read), which the bot token lacks. Idempotent + safe to
    // call from both the consent callback and host startup. Returns true only when a new webhook was created
    // (so the caller audits a real state change, not a no-op re-check — INV-005).
    Task<bool> EnsureRecordingsWebhookAsync(string accessToken, string targetUrl, string secret, CancellationToken ct = default);
}
