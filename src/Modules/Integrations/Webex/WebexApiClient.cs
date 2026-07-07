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

    public async Task<WebexRecording?> GetRecordingAsync(string recordingId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"recordings/{Uri.EscapeDataString(recordingId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.BotToken);

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
            start = start.UtcDateTime.ToString("o"),
            end = end.UtcDateTime.ToString("o"),
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
