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
            body = new System.IO.StreamReader(req.Content!.ReadAsStream()).ReadToEnd();
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

    private const string TargetUrl = "https://acmp.ngrok.dev/api/webex/webhook";

    [Fact]
    public async Task Ensure_recordings_webhook_creates_one_when_none_exist()
    {
        var calls = new List<(HttpMethod Method, string Path)>();
        var client = Client(req =>
        {
            calls.Add((req.Method, req.RequestUri!.AbsolutePath));
            return req.Method == HttpMethod.Get
                ? Json(HttpStatusCode.OK, "{\"items\":[]}")
                : new HttpResponseMessage(HttpStatusCode.OK);
        });

        var created = await client.EnsureRecordingsWebhookAsync("user-token", TargetUrl, "sekret");

        created.Should().BeTrue();
        calls.Should().ContainSingle(c => c.Method == HttpMethod.Post && c.Path.EndsWith("/webhooks"));
        calls.Should().NotContain(c => c.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task Ensure_recordings_webhook_is_a_noop_when_one_already_targets_the_url()
    {
        // A recordings hook already on our URL is kept; an unrelated messages hook is left untouched.
        var calls = new List<HttpMethod>();
        var client = Client(req =>
        {
            calls.Add(req.Method);
            return req.Method == HttpMethod.Get
                ? Json(HttpStatusCode.OK,
                    "{\"items\":[" +
                    "{\"id\":\"msg\",\"resource\":\"messages\",\"event\":\"created\",\"targetUrl\":\"" + TargetUrl + "\"}," +
                    "{\"id\":\"rec\",\"resource\":\"recordings\",\"event\":\"created\",\"targetUrl\":\"" + TargetUrl + "\"}]}")
                : new HttpResponseMessage(HttpStatusCode.OK);
        });

        var created = await client.EnsureRecordingsWebhookAsync("user-token", TargetUrl, "sekret");

        created.Should().BeFalse();
        calls.Should().Equal(HttpMethod.Get); // only the list — no POST, no DELETE
    }

    [Fact]
    public async Task Ensure_recordings_webhook_deletes_a_stale_url_then_creates()
    {
        var calls = new List<(HttpMethod Method, string Path)>();
        var client = Client(req =>
        {
            calls.Add((req.Method, req.RequestUri!.AbsolutePath));
            if (req.Method == HttpMethod.Get)
                return Json(HttpStatusCode.OK,
                    "{\"items\":[{\"id\":\"old\",\"resource\":\"recordings\",\"event\":\"created\"," +
                    "\"targetUrl\":\"https://old.ngrok.dev/api/webex/webhook\"}]}");
            return new HttpResponseMessage(HttpStatusCode.OK); // DELETE + POST both 200
        });

        var created = await client.EnsureRecordingsWebhookAsync("user-token", TargetUrl, "sekret");

        created.Should().BeTrue();
        calls.Should().Contain((HttpMethod.Delete, "/v1/webhooks/old"));
        calls.Should().Contain(c => c.Method == HttpMethod.Post && c.Path.EndsWith("/webhooks"));
    }

    [Fact]
    public async Task Ensure_recordings_webhook_tolerates_a_stale_hook_already_deleted()
    {
        // A concurrent reconcile may delete first — a 404 on DELETE is swallowed, and creation still proceeds.
        var client = Client(req =>
        {
            if (req.Method == HttpMethod.Get)
                return Json(HttpStatusCode.OK,
                    "{\"items\":[{\"id\":\"gone\",\"resource\":\"recordings\",\"event\":\"created\"," +
                    "\"targetUrl\":\"https://old.ngrok.dev/api/webex/webhook\"}]}");
            return new HttpResponseMessage(
                req.Method == HttpMethod.Delete ? HttpStatusCode.NotFound : HttpStatusCode.OK);
        });

        var created = await client.EnsureRecordingsWebhookAsync("user-token", TargetUrl, "sekret");
        created.Should().BeTrue();
    }

    // --- error / rate-limit branches on the remaining public methods ---

    [Fact]
    public async Task Get_recording_translates_429_and_other_errors()
    {
        var limited = Client(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        await limited.Invoking(c => c.GetRecordingAsync("user-token", "rec-1"))
            .Should().ThrowAsync<WebexRateLimitException>();

        var errored = Client(_ => Json(HttpStatusCode.InternalServerError, "{\"message\":\"boom\"}"));
        await errored.Invoking(c => c.GetRecordingAsync("user-token", "rec-1"))
            .Should().ThrowAsync<WebexApiException>();
    }

    [Fact]
    public async Task Create_meeting_translates_429_and_other_errors()
    {
        var limited = Client(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        await limited.Invoking(c => c.CreateMeetingAsync("user-token", "M", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)))
            .Should().ThrowAsync<WebexRateLimitException>();

        var errored = Client(_ => Json(HttpStatusCode.BadRequest, "{\"message\":\"bad\"}"));
        await errored.Invoking(c => c.CreateMeetingAsync("user-token", "M", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)))
            .Should().ThrowAsync<WebexApiException>();
    }

    [Fact]
    public async Task Ensure_recordings_webhook_propagates_list_429_and_errors()
    {
        var limited = Client(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)); // GET webhooks
        await limited.Invoking(c => c.EnsureRecordingsWebhookAsync("user-token", TargetUrl, "sekret"))
            .Should().ThrowAsync<WebexRateLimitException>();

        var errored = Client(_ => Json(HttpStatusCode.InternalServerError, "{}"));
        await errored.Invoking(c => c.EnsureRecordingsWebhookAsync("user-token", TargetUrl, "sekret"))
            .Should().ThrowAsync<WebexApiException>();
    }

    [Fact]
    public async Task Ensure_recordings_webhook_propagates_delete_429_and_errors()
    {
        // A stale-URL recordings hook forces a DELETE; a 429/5xx on it (not a 404) bubbles.
        static HttpResponseMessage StaleList() => new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"items\":[{\"id\":\"old\",\"resource\":\"recordings\",\"event\":\"created\"," +
                "\"targetUrl\":\"https://old.ngrok.dev/api/webex/webhook\"}]}", Encoding.UTF8, "application/json"),
        };

        var limited = Client(req => req.Method == HttpMethod.Get
            ? StaleList()
            : new HttpResponseMessage(HttpStatusCode.TooManyRequests)); // DELETE
        await limited.Invoking(c => c.EnsureRecordingsWebhookAsync("user-token", TargetUrl, "sekret"))
            .Should().ThrowAsync<WebexRateLimitException>();

        var errored = Client(req => req.Method == HttpMethod.Get
            ? StaleList()
            : Json(HttpStatusCode.InternalServerError, "{}")); // DELETE 500 (not 404)
        await errored.Invoking(c => c.EnsureRecordingsWebhookAsync("user-token", TargetUrl, "sekret"))
            .Should().ThrowAsync<WebexApiException>();
    }

    [Fact]
    public async Task Ensure_recordings_webhook_propagates_create_429_and_errors()
    {
        static HttpResponseMessage EmptyList() => new(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"items\":[]}", Encoding.UTF8, "application/json"),
        };

        var limited = Client(req => req.Method == HttpMethod.Get
            ? EmptyList()
            : new HttpResponseMessage(HttpStatusCode.TooManyRequests)); // POST create
        await limited.Invoking(c => c.EnsureRecordingsWebhookAsync("user-token", TargetUrl, "sekret"))
            .Should().ThrowAsync<WebexRateLimitException>();

        var errored = Client(req => req.Method == HttpMethod.Get
            ? EmptyList()
            : Json(HttpStatusCode.BadRequest, "{}")); // POST create 400
        await errored.Invoking(c => c.EnsureRecordingsWebhookAsync("user-token", TargetUrl, "sekret"))
            .Should().ThrowAsync<WebexApiException>();
    }

    // --- ReadRetryAfter branches: HTTP-date, bare-seconds fallback the typed header missed, default ---

    [Fact]
    public async Task Retry_after_uses_an_http_date_header()
    {
        var client = Client(_ =>
        {
            var r = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            r.Headers.TryAddWithoutValidation("Retry-After", "Wed, 21 Oct 2099 07:28:00 GMT");
            return r;
        });

        var ex = await client.Invoking(c => c.PostSpaceMessageAsync("{}")).Should().ThrowAsync<WebexRateLimitException>();
        ex.Which.RetryAfter.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task Retry_after_falls_back_to_a_bare_seconds_value_the_typed_header_could_not_parse()
    {
        // "+30" is not a valid delta-seconds token (typed RetryAfter stays null) but int.TryParse reads 30.
        var client = Client(_ =>
        {
            var r = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            r.Headers.TryAddWithoutValidation("Retry-After", "+30");
            return r;
        });

        var ex = await client.Invoking(c => c.PostSpaceMessageAsync("{}")).Should().ThrowAsync<WebexRateLimitException>();
        ex.Which.RetryAfter.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Retry_after_defaults_when_no_header_is_present()
    {
        var client = Client(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var ex = await client.Invoking(c => c.PostSpaceMessageAsync("{}")).Should().ThrowAsync<WebexRateLimitException>();
        ex.Which.RetryAfter.Should().Be(TimeSpan.FromSeconds(5)); // DefaultRetryAfter
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(_responder(request));
    }
}
