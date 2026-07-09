using Acmp.Modules.Integrations.Webex.Oauth;
using Acmp.Shared.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Acmp.Modules.Integrations.Webex;

// Keeps the recordings/created webhook registered so FR-057 auto-retrieval actually fires (the AC-070 live
// gap: inbound processing was built + unit-proven, but no webhook was ever created, so nothing fired). The
// static EnsureAsync is the shared routine — the OAuth callback calls it the moment a token is linked, and
// this BackgroundService re-runs it on host startup so a redeploy or ngrok-URL change re-registers. Runs in
// both API and worker hosts (registered in AddWebexIntegration); the reconcile converges to one webhook, so
// a concurrent double-run is harmless. Best-effort throughout: never throws to its caller (ADR-0024).
public sealed class WebexWebhookRegistrar : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<WebexWebhookRegistrar> _logger;

    public WebexWebhookRegistrar(IServiceProvider services, ILogger<WebexWebhookRegistrar> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _services.CreateScope();
        await EnsureAsync(scope.ServiceProvider, _logger, stoppingToken);
    }

    // Idempotent, best-effort registration from an already-scoped provider. No-ops (returns without throwing)
    // when Webex is off, no public URL is set, or no OAuth token exists yet — and swallows any failure so it
    // can neither break the OAuth callback nor crash host startup (a first-boot schema race, a missing token,
    // or a Webex 5xx all degrade to a logged no-op; the next consent or restart retries). Audits only a real
    // create (INV-005), not a no-op re-check.
    public static async Task EnsureAsync(IServiceProvider scoped, ILogger logger, CancellationToken ct)
    {
        try
        {
            var options = scoped.GetRequiredService<IOptions<WebexOptions>>().Value;
            if (!options.Enabled || string.IsNullOrWhiteSpace(options.WebhookPublicUrl))
                return;

            var accessToken = await scoped.GetRequiredService<IWebexTokenService>().GetValidAccessTokenAsync(ct);
            if (accessToken is null)
                return; // consent not completed — nothing to register against yet

            var targetUrl = $"{options.WebhookPublicUrl.TrimEnd('/')}/api/webex/webhook";
            var created = await scoped.GetRequiredService<IWebexApiClient>()
                .EnsureRecordingsWebhookAsync(accessToken, targetUrl, options.WebhookSecret, ct);

            if (created)
                await scoped.GetRequiredService<IAuditSink>()
                    .EmitAsync("Webex.RecordingWebhookRegistered", "system:webex", new { targetUrl }, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Webex recordings-webhook registration failed; retrying on next consent or restart");
        }
    }
}
