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
}
