using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Acmp.Modules.Integrations.Webex;
using Acmp.Modules.Integrations.Webex.Oauth;
using Acmp.Shared.Authorization;
using Microsoft.Extensions.Options;

namespace Acmp.Api.Endpoints;

// Inbound Webex webhook. The ONLY anonymous endpoint (no auth FallbackPolicy is configured, so omitting
// RequireAuthorization leaves it open) — authenticated instead by the HMAC WebexSignatureFilter that runs
// first. The handler returns 200 immediately and enqueues processing; it never does DB work inline and never
// throws back to Webex. Replay guard: events older than 5 minutes are dropped (Webex guidance).
public static class WebexEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);

    public static IEndpointRouteBuilder MapWebexEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/webex/webhook", Handle)
            .WithTags("Webex")
            .AddEndpointFilter<WebexSignatureFilter>();

        // OAuth consent flow for the secretary meeting-create token (WS3b). Admin-gated: an administrator
        // completes this once on a private-network device with reach to Webex.
        var oauth = app.MapGroup("/api/webex/oauth").WithTags("Webex").RequireAuthorization(Policies.AdminConfig);
        oauth.MapGet("/start", StartOAuth);
        oauth.MapGet("/callback", CallbackAsync);
        return app;
    }

    private const string StateCookie = "webex_oauth_state";

    private static IResult StartOAuth(HttpContext http, IOptions<WebexOptions> options)
    {
        var o = options.Value;
        if (!o.Enabled)
            return Results.BadRequest("Webex is not enabled.");

        // Bind a single-use OAuth `state` to the admin's browser to defend against OAuth CSRF /
        // authorization-code injection: the callback must echo this exact value (ADR-0023 security note).
        // The Admin gate controls who reaches the endpoint, not which code is submitted — so state is required.
        var state = Guid.NewGuid().ToString("N");
        http.Response.Cookies.Append(StateCookie, state, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(10),
        });

        var authorizeUrl =
            $"{o.ApiBaseUrl.TrimEnd('/')}/authorize?client_id={Uri.EscapeDataString(o.OAuthClientId)}" +
            $"&response_type=code&redirect_uri={Uri.EscapeDataString(o.OAuthRedirectUri)}" +
            $"&scope={Uri.EscapeDataString(o.OAuthScopes)}&state={state}";
        return Results.Redirect(authorizeUrl);
    }

    private static async Task<IResult> CallbackAsync(HttpContext http, string? code, string? state, IOptions<WebexOptions> options, CancellationToken ct)
    {
        if (!options.Value.Enabled)
            return Results.BadRequest("Webex is not enabled.");
        if (string.IsNullOrWhiteSpace(code))
            return Results.BadRequest("Missing authorization code.");

        // OAuth CSRF guard: the returned state must match the single-use cookie value (constant-time), then
        // consume the cookie. A missing/mismatched state is rejected before any token exchange.
        var expected = http.Request.Cookies[StateCookie];
        http.Response.Cookies.Delete(StateCookie);
        if (string.IsNullOrEmpty(expected) || string.IsNullOrWhiteSpace(state) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(state)))
            return Results.BadRequest("Invalid or missing OAuth state.");

        var oauth = http.RequestServices.GetRequiredService<IWebexOAuthClient>();
        var tokens = http.RequestServices.GetRequiredService<IWebexTokenService>();

        var token = await oauth.ExchangeCodeAsync(code, ct);
        if (token?.AccessToken is null)
            return Results.BadRequest("Webex token exchange failed.");

        await tokens.StoreFromExchangeAsync(token, ct);
        return Results.Content("Webex integration authorized. You can close this window.");
    }

    private static IResult Handle(HttpContext http, IOptions<WebexOptions> options)
    {
        if (!options.Value.Enabled)
            return Results.Ok();

        if (http.Items["webex-body"] is not string body || string.IsNullOrEmpty(body))
            return Results.Ok();

        WebexWebhookEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<WebexWebhookEnvelope>(body, Json);
        }
        catch (JsonException)
        {
            return Results.Ok(); // never 500 to Webex on a malformed body
        }

        if (envelope is null)
            return Results.Ok();

        // Replay guard.
        if (envelope.Created is { } created && DateTimeOffset.UtcNow - created > MaxAge)
            return Results.Ok();

        if (string.Equals(envelope.Resource, "recordings", StringComparison.OrdinalIgnoreCase)
            && string.Equals(envelope.Event, "created", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(envelope.Data?.Id))
        {
            var recordingId = envelope.Data!.Id!;
            // Resolve the scheduler only once we know Webex is enabled (it is unregistered when disabled).
            var scheduler = http.RequestServices.GetRequiredService<IWebexJobScheduler>();
            scheduler.Enqueue<WebexWebhookJob>(job => job.ProcessRecordingAsync(recordingId, CancellationToken.None));
        }

        return Results.Ok();
    }
}

// The subset of the Webex webhook payload ACMP consumes (developer.webex.com webhooks guide).
public sealed record WebexWebhookEnvelope
{
    public string? Resource { get; init; }
    public string? Event { get; init; }
    public DateTimeOffset? Created { get; init; }

    [JsonPropertyName("data")]
    public WebexWebhookData? Data { get; init; }
}

public sealed record WebexWebhookData
{
    public string? Id { get; init; }
    public string? MeetingId { get; init; }
}
