using Acmp.Bootstrap;
using Acmp.Modules.Integrations.Webex;
using Acmp.Shared.Contracts.Meetings;
using FluentAssertions;
using Hangfire;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Acmp.Application.Tests.Bootstrap;

// WS0 / ADR-0024: the worker builds its ENTIRE service graph from the shared AddAcmpModules — no host-specific
// wiring. If that composition root ever drops a registration a worker job needs, the failure would surface only
// at runtime inside the container. These tests turn that into a build-time check by constructing the exact job
// graph the worker runs.
public class CompositionRootTests
{
    private static ServiceProvider Build(bool webexEnabled)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Acmp"] = "Server=test;Database=Acmp;Trusted_Connection=True;TrustServerCertificate=True",
            ["Webex:Enabled"] = webexEnabled ? "true" : "false",
            ["Webex:BotToken"] = "test-bot",
            ["Webex:WebhookSecret"] = "test-secret",
            ["Webex:ApiBaseUrl"] = "https://webexapis.com/v1",
            ["Webex:TokenEncryptionKey"] = "test-key-0123456789",
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAcmpModules(config);
        // The worker gets IBackgroundJobClient from AddHangfireServer; the DI graph only needs it resolvable (the
        // Webex scheduler wraps it), so a fake stands in — no SQL / Hangfire server required for a wiring check.
        services.AddSingleton(Substitute.For<IBackgroundJobClient>());
        return services.BuildServiceProvider();
    }

    [Fact] // The recurring action-reminder sweep the worker cron-triggers dispatches via MediatR.
    public void Composition_root_resolves_the_mediatr_pipeline()
    {
        using var provider = Build(webexEnabled: false);
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ISender>().Should().NotBeNull();
    }

    [Fact] // Constructs each Webex job with its real dependency graph — the same the worker's Hangfire activator does.
    public void Composition_root_resolves_every_webex_job_when_enabled()
    {
        using var provider = Build(webexEnabled: true);
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetRequiredService<WebexSendJob>().Should().NotBeNull();
        sp.GetRequiredService<WebexWebhookJob>().Should().NotBeNull();
        sp.GetRequiredService<WebexMeetingCreateJob>().Should().NotBeNull();
    }

    [Fact] // Order guard: Webex composes AFTER Meetings, so its provisioner overrides the module's no-op default.
    public void Webex_provisioner_overrides_the_meetings_default_when_enabled()
    {
        using var provider = Build(webexEnabled: true);
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IWebexMeetingProvisioner>()
            .Should().BeOfType<WebexMeetingProvisioner>();
    }

    [Fact] // AC-071 mirror at the worker: Webex disabled => the adapter registers nothing, so no send job exists.
    public void Composition_root_registers_no_webex_job_when_disabled()
    {
        using var provider = Build(webexEnabled: false);
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetService<WebexSendJob>().Should().BeNull();
    }
}
