using Acmp.Modules.Integrations.Webex.Oauth;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Meetings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Acmp.Modules.Integrations.Webex;

// Single entry point both hosts (API + worker) call to wire the Webex adapter. When Webex:Enabled is false
// the adapter is NOT registered at all (AC-071): no sink, no client, no job — in-app stays the sole channel
// and nothing can make an outbound call. Options are always bound so the (always-mapped) webhook endpoint can
// read Enabled/secret and short-circuit.
public static class WebexIntegrationExtensions
{
    public static IServiceCollection AddWebexIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(WebexOptions.SectionName);
        // Bind + fail-closed validation on start (M1): a placeholder/weak TokenEncryptionKey while Enabled is a
        // boot failure, never a silent at-rest token exposure. Validation is a no-op when disabled.
        services.AddOptions<WebexOptions>().Bind(section).ValidateOnStart();
        services.AddSingleton<IValidateOptions<WebexOptions>, WebexOptionsValidator>();

        var options = section.Get<WebexOptions>() ?? new WebexOptions();
        if (!options.Enabled)
            return services;

        services.AddHttpClient<IWebexApiClient, WebexApiClient>(client =>
        {
            client.BaseAddress = new Uri(EnsureTrailingSlash(options.ApiBaseUrl));
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddScoped<IWebexJobScheduler, HangfireWebexJobScheduler>();
        services.AddScoped<INotificationSink, WebexNotificationSink>();
        services.AddScoped<WebexSendJob>();
        services.AddScoped<WebexWebhookJob>();

        // OAuth token store + meeting auto-create (WS3b). The DbContext lives in its own "webex" schema and is
        // provisioned by the API's MigrationRunner.
        services.AddDbContext<WebexDbContext>(db =>
            db.UseSqlServer(configuration.GetConnectionString("Acmp"),
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", WebexDbContext.Schema)));
        services.AddSingleton<WebexTokenProtector>();
        services.AddHttpClient<IWebexOAuthClient, WebexOAuthClient>(client =>
        {
            client.BaseAddress = new Uri(EnsureTrailingSlash(options.ApiBaseUrl));
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IWebexTokenService, WebexTokenService>();
        services.AddScoped<WebexMeetingCreateJob>();
        // Overrides the Meetings module's no-op default (registered earlier in composition) when enabled.
        services.AddScoped<IWebexMeetingProvisioner, WebexMeetingProvisioner>();
        // Re-registers the recordings webhook on host startup (redeploy / ngrok-URL change). Best-effort;
        // no-ops until a token exists. Runs in both hosts — the reconcile converges, so that is harmless.
        services.AddHostedService<WebexWebhookRegistrar>();

        return services;
    }

    private static string EnsureTrailingSlash(string url) => url.EndsWith('/') ? url : url + "/";
}
