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

    // The BackgroundService override: ExecuteAsync opens a DI scope and delegates to EnsureAsync. Deps must be
    // SCOPED (ExecuteAsync calls CreateScope), so this also proves the scope resolves what EnsureAsync asks for.
    // ⚠⚠ NEITHER LIFECYCLE METHOD IS A JOIN, AND BOTH FAIL SILENTLY (DEF-113). Since .NET 10 the WHOLE of
    // ExecuteAsync is dispatched to a background thread, so StartAsync returns with ExecuteTask still
    // WaitingForActivation — an assertion straight after it is evaluated against work that has not happened.
    // StopAsync is no better: it cancels the stopping token BEFORE awaiting, and the body is dispatched with
    // that same token, so on a loaded runner it can cancel the work before it ever starts and then "join" a
    // task that did nothing. ExecuteTask IS the running body: awaiting it involves no cancellation, so it is
    // deterministic by construction rather than by timing, and it rethrows whatever ExecuteAsync threw, which
    // is what makes the never-throws claim real. Asserting the API call POSITIVELY is what keeps the pass
    // non-hollow — a negative assertion is satisfied by the empty run and cannot tell the two apart.
    [Fact]
    public async Task Background_service_runs_ensure_within_a_scope_and_never_throws()
    {
        var api = Substitute.For<IWebexApiClient>();
        var tokens = Substitute.For<IWebexTokenService>();
        tokens.GetValidAccessTokenAsync(Arg.Any<CancellationToken>()).Returns("user-token");
        var audit = Substitute.For<IAuditSink>();

        var services = new ServiceCollection();
        services.AddScoped<IOptions<WebexOptions>>(_ => Options.Create(Enabled()));
        services.AddScoped(_ => tokens);
        services.AddScoped(_ => api);
        services.AddScoped(_ => audit);
        using var sp = services.BuildServiceProvider();

        var registrar = new WebexWebhookRegistrar(sp, NullLogger<WebexWebhookRegistrar>.Instance);
        var hosted = (IHostedService)registrar;

        await hosted.StartAsync(CancellationToken.None);
        await FluentActions.Awaiting(() => registrar.ExecuteTask!).Should().NotThrowAsync();

        // The registrar really drove the shared routine through its own scope, at the derived URL.
        await api.Received(1).EnsureRecordingsWebhookAsync("user-token", Url, "sekret", Arg.Any<CancellationToken>());
    }
}
