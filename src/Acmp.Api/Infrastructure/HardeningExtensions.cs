using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;

namespace Acmp.Api.Infrastructure;

// P16-B4 request-pipeline hardening (C-API-03 rate limiting, C-CON-003 read-only-FS DataProtection).
//
// Rate limiting (C-API-03) is PROPORTIONAL — "tuned for ~15 concurrent users, not anti-DDoS"
// (docs/domain/security-controls.md). Authenticated traffic is partitioned by the caller's `sub` (fairer than
// by IP — no shared-NAT penalty, and it sidesteps trusting proxy-forwarded IPs behind nginx); the anonymous
// Webex webhook gets one global bucket. Policies are applied per-endpoint via .RequireRateLimiting(...).
public static class HardeningExtensions
{
    public static IServiceCollection AddAcmpRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
            ?? new RateLimitingOptions();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = (context, cancellationToken) =>
            {
                // Advertise when to retry (fixed-window => the window length) so clients back off politely.
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };

            limiter.AddPolicy(RateLimitPolicies.Search, http =>
                PerUserFixedWindow(http, options.SearchPermitPerMinute));
            limiter.AddPolicy(RateLimitPolicies.Upload, http =>
                PerUserFixedWindow(http, options.UploadPermitPerMinute));
            // The webhook is anonymous (no `sub`) — a single global bucket bounds ingestion volume.
            limiter.AddPolicy(RateLimitPolicies.Webhook, _ =>
                RateLimitPartition.GetFixedWindowLimiter("webhook-global", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.WebhookPermitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

        return services;
    }

    private static RateLimitPartition<string> PerUserFixedWindow(HttpContext http, int permitPerMinute)
    {
        // `sub` for an authenticated caller; the connection's remote IP is the fallback for the rare
        // unauthenticated hit (which auth would 401 anyway before the endpoint runs).
        var key = http.User.FindFirstValue("sub")
            ?? http.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? http.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    }

    // C-CON-003: the API runs on a read-only root filesystem in production. ASP.NET's DataProtection key-ring
    // defaults to persisting keys on the container FS — which now throws on write. Point it at a writable path
    // (a tmpfs mount, set via DataProtection:KeysPath) when configured; empty keeps the framework default for
    // dev / non-container runs. Keys need not survive a restart (bearer auth — no antiforgery/session payloads
    // are persisted), so an ephemeral tmpfs is sufficient.
    public static IServiceCollection AddAcmpDataProtection(this IServiceCollection services, IConfiguration configuration)
    {
        var keysPath = configuration["DataProtection:KeysPath"];
        var builder = services.AddDataProtection().SetApplicationName("acmp");
        if (!string.IsNullOrWhiteSpace(keysPath))
            builder.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        return services;
    }
}

public static class RateLimitPolicies
{
    public const string Search = "acmp-search";
    public const string Upload = "acmp-upload";
    public const string Webhook = "acmp-webhook";
}

// Proportional defaults (C-API-03 — ~15 users, not anti-DDoS); overridable via the "RateLimiting" config section.
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";
    public int SearchPermitPerMinute { get; init; } = 60;
    public int UploadPermitPerMinute { get; init; } = 20;
    public int WebhookPermitPerMinute { get; init; } = 120;
}
