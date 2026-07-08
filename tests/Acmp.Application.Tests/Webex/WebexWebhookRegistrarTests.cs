using Acmp.Modules.Integrations.Webex;
using Acmp.Modules.Integrations.Webex.Oauth;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Acmp.Application.Tests.Webex;

// The static EnsureAsync is the shared registration routine (OAuth callback + startup registrar). It must be
// idempotent-audited (only a real create logs an AuditEvent), no-op when it cannot act, and never throw.
public class WebexWebhookRegistrarTests
{
    private const string Url = "https://acmp.ngrok.dev/api/webex/webhook";

    private static (IServiceProvider Sp, IWebexApiClient Api, IAuditSink Audit) Build(WebexOptions options, string? token)
    {
        var api = Substitute.For<IWebexApiClient>();
        var tokens = Substitute.For<IWebexTokenService>();
        tokens.GetValidAccessTokenAsync(Arg.Any<CancellationToken>()).Returns(token);
        var audit = Substitute.For<IAuditSink>();

        var services = new ServiceCollection();
        services.AddSingleton<IOptions<WebexOptions>>(Options.Create(options));
        services.AddSingleton(api);
        services.AddSingleton(tokens);
        services.AddSingleton(audit);
        return (services.BuildServiceProvider(), api, audit);
    }

    private static WebexOptions Enabled() =>
        new() { Enabled = true, WebhookPublicUrl = "https://acmp.ngrok.dev", WebhookSecret = "sekret" };

    private static Task Run(IServiceProvider sp) =>
        WebexWebhookRegistrar.EnsureAsync(sp, NullLogger.Instance, CancellationToken.None);

    [Fact]
    public async Task Registers_at_the_derived_url_and_audits_a_real_create()
    {
        var (sp, api, audit) = Build(Enabled(), "user-token");
        api.EnsureRecordingsWebhookAsync("user-token", Url, "sekret", Arg.Any<CancellationToken>()).Returns(true);

        await Run(sp);

        await api.Received(1).EnsureRecordingsWebhookAsync("user-token", Url, "sekret", Arg.Any<CancellationToken>());
        await audit.Received(1).EmitAsync("Webex.RecordingWebhookRegistered", "system:webex",
            Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Does_not_audit_when_the_webhook_already_existed()
    {
        var (sp, api, audit) = Build(Enabled(), "user-token");
        api.EnsureRecordingsWebhookAsync(default!, default!, default!, default).ReturnsForAnyArgs(false);

        await Run(sp);

        await audit.DidNotReceive().EmitAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Skips_when_disabled()
    {
        var (sp, api, _) = Build(new WebexOptions { Enabled = false }, "user-token");
        await Run(sp);
        await api.DidNotReceiveWithAnyArgs().EnsureRecordingsWebhookAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Skips_when_no_public_url_is_configured()
    {
        var (sp, api, _) = Build(new WebexOptions { Enabled = true, WebhookPublicUrl = "" }, "user-token");
        await Run(sp);
        await api.DidNotReceiveWithAnyArgs().EnsureRecordingsWebhookAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Skips_when_no_oauth_token_exists_yet()
    {
        var (sp, api, _) = Build(Enabled(), token: null);
        await Run(sp);
        await api.DidNotReceiveWithAnyArgs().EnsureRecordingsWebhookAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Swallows_a_webex_failure_so_it_never_breaks_the_caller()
    {
        var (sp, api, audit) = Build(Enabled(), "user-token");
        api.EnsureRecordingsWebhookAsync(default!, default!, default!, default)
            .ThrowsAsyncForAnyArgs(new WebexApiException(500, "boom"));

        await Run(sp); // must not throw

        await audit.DidNotReceive().EmitAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    // The BackgroundService override: StartAsync runs ExecuteAsync, which opens a DI scope and delegates to
    // EnsureAsync. Deps must be SCOPED (ExecuteAsync calls CreateScope). Disabled options → a quick no-op.
    [Fact]
    public async Task Background_service_runs_ensure_within_a_scope_and_never_throws()
    {
        var api = Substitute.For<IWebexApiClient>();
        var tokens = Substitute.For<IWebexTokenService>();
        var audit = Substitute.For<IAuditSink>();

        var services = new ServiceCollection();
        services.AddScoped<IOptions<WebexOptions>>(_ => Options.Create(new WebexOptions { Enabled = false }));
        services.AddScoped(_ => tokens);
        services.AddScoped(_ => api);
        services.AddScoped(_ => audit);
        using var sp = services.BuildServiceProvider();

        var registrar = new WebexWebhookRegistrar(sp, NullLogger<WebexWebhookRegistrar>.Instance);

        await ((IHostedService)registrar).Invoking(r => r.StartAsync(CancellationToken.None)).Should().NotThrowAsync();

        // Disabled → EnsureAsync no-ops before touching the API.
        await api.DidNotReceiveWithAnyArgs().EnsureRecordingsWebhookAsync(default!, default!, default!, default);
    }
}
