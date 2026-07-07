using System.Net;
using System.Text;
using Acmp.Modules.Integrations.Webex;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Acmp.Application.Tests.Webex;

// Exercises the concrete WebexApiClient over a stubbed HttpMessageHandler: success, 429 → typed
// rate-limit exception carrying Retry-After, other errors → typed API exception, and JSON parsing.
public class WebexApiClientTests
{
    private static WebexApiClient Client(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        new(new HttpClient(new StubHandler(responder)) { BaseAddress = new Uri("https://webexapis.com/v1/") },
            Options.Create(new WebexOptions { BotToken = "bot-token" }));

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Post_space_message_succeeds_on_200()
    {
        var client = Client(_ => new HttpResponseMessage(HttpStatusCode.OK));
        await client.Invoking(c => c.PostSpaceMessageAsync("{}")).Should().NotThrowAsync();
    }

    [Fact]
    public async Task Post_space_message_translates_429_to_a_rate_limit_exception_with_retry_after()
    {
        var client = Client(_ =>
        {
            var r = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            r.Headers.TryAddWithoutValidation("Retry-After", "30");
            return r;
        });

        var ex = await client.Invoking(c => c.PostSpaceMessageAsync("{}")).Should().ThrowAsync<WebexRateLimitException>();
        ex.Which.RetryAfter.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Post_space_message_throws_api_exception_on_other_errors()
    {
        var client = Client(_ => Json(HttpStatusCode.BadRequest, "{\"message\":\"bad\"}"));
        await client.Invoking(c => c.PostSpaceMessageAsync("{}")).Should().ThrowAsync<WebexApiException>();
    }

    [Fact]
    public async Task Get_recording_parses_the_reference_and_returns_null_on_404()
    {
        var found = Client(_ => Json(HttpStatusCode.OK,
            "{\"id\":\"rec-1\",\"meetingId\":\"webex-abc\",\"playbackUrl\":\"https://play\",\"downloadUrl\":\"https://dl\",\"durationSeconds\":1800}"));
        var rec = await found.GetRecordingAsync("user-token", "rec-1");
        rec!.MeetingId.Should().Be("webex-abc");
        rec.PlaybackUrl.Should().Be("https://play");
        rec.DurationSeconds.Should().Be(1800);

        var missing = Client(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        (await missing.GetRecordingAsync("user-token", "gone")).Should().BeNull();
    }

    [Fact]
    public async Task Create_meeting_parses_id_and_sends_seconds_precision_utc_dates()
    {
        string? body = null;
        var client = Client(req =>
        {
            body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json(HttpStatusCode.OK, "{\"id\":\"webex-9\",\"webLink\":\"https://webex/join\"}");
        });

        var start = new DateTimeOffset(2026, 7, 10, 6, 0, 0, TimeSpan.Zero);
        var created = await client.CreateMeetingAsync("user-token", "Committee", start, start.AddHours(1));

        created!.Id.Should().Be("webex-9");
        created.JoinUrl.Should().Be("https://webex/join");
        // Seconds-precision UTC — the "o" format's 7 fractional digits get a Webex 400 (caught in the live sandbox).
        body.Should().Contain("\"start\":\"2026-07-10T06:00:00Z\"").And.Contain("\"end\":\"2026-07-10T07:00:00Z\"");
        body.Should().NotContain(".0000000");
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(_responder(request));
    }
}
