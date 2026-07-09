using System.Diagnostics.CodeAnalysis;
using Acmp.Modules.Actions.Application;
using Acmp.Modules.Actions.Application.Reminders;
using Acmp.Modules.Actions.Infrastructure;
using Acmp.Modules.Decisions.Application;
using Acmp.Modules.Decisions.Infrastructure;
using Acmp.Modules.Dependencies.Application;
using Acmp.Modules.Dependencies.Infrastructure;
using Acmp.Modules.Governance.Application;
using Acmp.Modules.Governance.Infrastructure;
using Acmp.Modules.Integrations.Webex;
using Acmp.Modules.Meetings.Application;
using Acmp.Modules.Meetings.Infrastructure;
using Acmp.Modules.Membership.Application;
using Acmp.Modules.Membership.Infrastructure;
using Acmp.Modules.Notifications.Application;
using Acmp.Modules.Notifications.Infrastructure;
using Acmp.Modules.Risks.Application;
using Acmp.Modules.Risks.Infrastructure;
using Acmp.Modules.Topics.Application;
using Acmp.Modules.Topics.Infrastructure;
using Acmp.Modules.Traceability.Application;
using Acmp.Modules.Traceability.Infrastructure;
using Acmp.Shared;
using Hangfire;
using Hangfire.SqlServer;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Bootstrap;

// The single module-wiring surface shared by the API and the worker (ADR-0024). Keeping it here — not
// duplicated in each host's Program.cs — is what guarantees the two processes resolve an identical service
// graph, so a job enqueued by the API constructs correctly when the worker runs it.
public static class AcmpCompositionRoot
{
    public static IServiceCollection AddAcmpModules(this IServiceCollection services, IConfiguration configuration)
    {
        // Shared kernel (clock, current-user, file store, MediatR behaviors) + every module.
        services.AddSharedKernel(configuration);
        services.AddMembershipModule(configuration);
        services.AddTopicsModule(configuration);
        services.AddMeetingsModule(configuration);
        services.AddDecisionsModule(configuration);
        services.AddActionsModule(configuration);
        services.AddRisksModule(configuration);
        services.AddTraceabilityModule(configuration);
        services.AddDependenciesModule(configuration);
        services.AddGovernanceModule(configuration);
        services.AddNotificationsModule(configuration);
        // Webex adapter (P13, ADR-0005): a second INotificationSink behind the dispatcher, and the meeting
        // provisioner. MUST come AFTER Meetings — its IWebexMeetingProvisioner overrides the Meetings no-op
        // default by last-registration-wins. Registers nothing when Webex:Enabled is false (AC-071).
        services.AddWebexIntegration(configuration);

        // One MediatR registration over the shared + module application assemblies.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
            typeof(SharedKernelExtensions).Assembly,
            MembershipApplicationExtensions.Assembly,
            TopicsApplicationExtensions.Assembly,
            MeetingsApplicationExtensions.Assembly,
            DecisionsApplicationExtensions.Assembly,
            ActionsApplicationExtensions.Assembly,
            RisksApplicationExtensions.Assembly,
            TraceabilityApplicationExtensions.Assembly,
            DependenciesApplicationExtensions.Assembly,
            GovernanceApplicationExtensions.Assembly,
            NotificationsApplicationExtensions.Assembly));

        // Action reminder/escalation knobs (docs/domain/notification-strategy.md §3.4). The worker runs the
        // recurring sweep; binding here keeps both hosts' options identical.
        services.Configure<ActionReminderOptions>(
            configuration.GetSection(ActionReminderOptions.SectionName));

        return services;
    }

    // App-owned Hangfire on ACMP's OWN SQL (ADR-0014, CON-001): its schema bootstraps its own tables and never
    // shares ACMP's domain tables. Identical config on both sides — the API registers ONLY this (enqueue-only,
    // IBackgroundJobClient); the worker adds AddHangfireServer() on top to process jobs (ADR-0024).
    // ExcludeFromCodeCoverage: un-assertable plumbing (like MinioFileStore/Program) — SqlServerStorage's ctor
    // eagerly opens a SQL connection, so it can't run under the InMemory test host; it's skipped at Testing boot.
    [ExcludeFromCodeCoverage]
    public static IServiceCollection AddAcmpHangfireStorage(this IServiceCollection services, string connectionString)
    {
        services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                SchemaName = "HangFire",
                PrepareSchemaIfNecessary = true,
            }));
        return services;
    }
}
