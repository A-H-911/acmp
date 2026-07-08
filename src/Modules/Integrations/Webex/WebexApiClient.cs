using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;

namespace Acmp.Modules.Integrations.Webex;

// Typed HttpClient over the Webex REST API. Base address + timeout are set at registration; the bot token is
// attached per request from options so a rotated token takes effect without re-creating the client.
public sealed class WebexApiClient : IWebexApiClient
{
    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(5);

    // Webex meetings API accepts ISO 8601 but rejects the "o" round-trip format's 7 fractional-second digits.
    private const string WebexDateFormat = "yyyy-MM-ddTHH:mm:ss'Z'";

    private readonly HttpClient _http;
    private readonly WebexOptions _options;

    public WebexApiClient(HttpClient http, IOptions<WebexOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task PostSpaceMessageAsync(string messageRequestJson, CancellationToken ct = default)
    {
        using var content = new StringContent(messageRequestJson, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, "messages") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.BotToken);

        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new WebexRateLimitException(ReadRetryAfter(response));
        if (!response.IsSuccessStatusCode)
            throw new WebexApiException((int)response.StatusCode, await SafeBodyAsync(response, ct));
    }

    public async Task<WebexRecording?> GetRecordingAsync(string accessToken, string recordingId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"recordings/{Uri.EscapeDataString(recordingId)}");
        // Recordings are user-scoped — authenticate with the OAuth host token, NOT the bot token (Webex 403s it).
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new WebexRateLimitException(ReadRetryAfter(response));
        if (!response.IsSuccessStatusCode)
            throw new WebexApiException((int)response.StatusCode, await SafeBodyAsync(response, ct));

        var dto = await response.Content.ReadFromJsonAsync<RecordingDto>(cancellationToken: ct);
        return dto is null
            ? null
            : new WebexRecording(dto.Id ?? recordingId, dto.MeetingId, dto.PlaybackUrl, dto.DownloadUrl, dto.DurationSeconds);
    }

    private sealed record RecordingDto
    {
        public string? Id { get; init; }
        public string? MeetingId { get; init; }
        public string? PlaybackUrl { get; init; }
        public string? DownloadUrl { get; init; }
        public int? DurationSeconds { get; init; }
    }

    public async Task<CreatedWebexMeeting?> CreateMeetingAsync(string accessToken, string title,
        DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
    {
        var payload = new
        {
            title,
            // Seconds-precision UTC ISO 8601 (e.g. 2026-07-10T06:00:00Z). NOT the round-trip "o" format — its
            // 7-digit fractional seconds are rejected by the Webex meetings API with HTTP 400 (confirmed live).
            start = start.UtcDateTime.ToString(WebexDateFormat, CultureInfo.InvariantCulture),
            end = end.UtcDateTime.ToString(WebexDateFormat, CultureInfo.InvariantCulture),
        };
        using var content = JsonContent.Create(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, "meetings") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new WebexRateLimitException(ReadRetryAfter(response));
        if (!response.IsSuccessStatusCode)
            throw new WebexApiException((int)response.StatusCode, await SafeBodyAsync(response, ct));

        var dto = await response.Content.ReadFromJsonAsync<MeetingDto>(cancellationToken: ct);
        return dto?.Id is null ? null : new CreatedWebexMeeting(dto.Id, dto.WebLink ?? string.Empty);
    }

    private sealed record MeetingDto
    {
        public string? Id { get; init; }
        public string? WebLink { get; init; }
    }

    public async Task<bool> EnsureRecordingsWebhookAsync(string accessToken, string targetUrl, string secret, CancellationToken ct = default)
    {
        var existing = await ListWebhooksAsync(accessToken, ct);
        var recordingHooks = existing
            .Where(w => string.Equals(w.Resource, "recordings", StringComparison.OrdinalIgnoreCase)
                     && string.Equals(w.Event, "created", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Keep at most one hook already on the current targetUrl (assumed ours — the secret is redacted on
        // read so we cannot re-verify it); delete every other recordings hook (stale ngrok URLs + any
        // duplicate a concurrent API/worker registration created). Converges to exactly one.
        var keep = recordingHooks.FirstOrDefault(w =>
            string.Equals(w.TargetUrl, targetUrl, StringComparison.OrdinalIgnoreCase));
        foreach (var hook in recordingHooks)
        {
            if (ReferenceEquals(hook, keep) || string.IsNullOrEmpty(hook.Id))
                continue;
            await DeleteWebhookAsync(accessToken, hook.Id!, ct);
        }

        if (keep is not null)
            return false;

        var payload = new
        {
            name = "acmp-recordings-created",
            resource = "recordings",
            @event = "created",
            targetUrl,
            secret,
        };
        using var content = JsonContent.Create(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, "webhooks") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new WebexRateLimitException(ReadRetryAfter(response));
        if (!response.IsSuccessStatusCode)
            throw new WebexApiException((int)response.StatusCode, await SafeBodyAsync(response, ct));
        return true;
    }

    private async Task<IReadOnlyList<WebhookDto>> ListWebhooksAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "webhooks");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new WebexRateLimitException(ReadRetryAfter(response));
        if (!response.IsSuccessStatusCode)
            throw new WebexApiException((int)response.StatusCode, await SafeBodyAsync(response, ct));

        var list = await response.Content.ReadFromJsonAsync<WebhookListDto>(cancellationToken: ct);
        return list?.Items ?? [];
    }

    private async Task DeleteWebhookAsync(string accessToken, string id, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"webhooks/{Uri.EscapeDataString(id)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.NotFound)
            return; // already gone — a concurrent reconcile beat us to it
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new WebexRateLimitException(ReadRetryAfter(response));
        if (!response.IsSuccessStatusCode)
            throw new WebexApiException((int)response.StatusCode, await SafeBodyAsync(response, ct));
    }

    private sealed record WebhookListDto
    {
        public IReadOnlyList<WebhookDto>? Items { get; init; }
    }

    private sealed record WebhookDto
    {
        public string? Id { get; init; }
        public string? TargetUrl { get; init; }
        public string? Resource { get; init; }
        public string? Event { get; init; }
    }

    private static TimeSpan ReadRetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta) return delta;
        if (header?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero) return wait;
        }
        // Some gateways send a bare seconds value that HttpClient could not parse into RetryAfter.
        if (response.Headers.TryGetValues("Retry-After", out var values)
            && int.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            && seconds > 0)
            return TimeSpan.FromSeconds(seconds);
        return DefaultRetryAfter;
    }

    private static async Task<string> SafeBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return body.Length > 500 ? body[..500] : body;
        }
        catch
        {
            return "<unreadable body>";
        }
    }
}
