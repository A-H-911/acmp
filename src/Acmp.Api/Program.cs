using Acmp.Api.Endpoints;
using Acmp.Api.Infrastructure;
using Acmp.Api.Infrastructure.Authentication;
using Acmp.Modules.Actions.Application;
using Acmp.Modules.Actions.Application.Reminders;
using Acmp.Modules.Actions.Infrastructure;
using Acmp.Modules.Decisions.Application;
using Acmp.Modules.Decisions.Infrastructure;
using Acmp.Modules.Dependencies.Application;
using Acmp.Modules.Dependencies.Infrastructure;
using Acmp.Modules.Governance.Application;
using Acmp.Modules.Governance.Infrastructure;
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
using Acmp.Shared.Authorization;
using Hangfire;
using Hangfire.SqlServer;
using MediatR;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog -> console + self-hosted Seq (ADR-0014). Fully configuration-driven.
builder.Host.UseSerilog((context, services, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// Shared kernel (clock, current-user, file store, MediatR behaviors) + modules.
builder.Services.AddSharedKernel(builder.Configuration);
builder.Services.AddMembershipModule(builder.Configuration);
builder.Services.AddTopicsModule(builder.Configuration);
builder.Services.AddMeetingsModule(builder.Configuration);
builder.Services.AddDecisionsModule(builder.Configuration);
builder.Services.AddActionsModule(builder.Configuration);
builder.Services.AddRisksModule(builder.Configuration);
builder.Services.AddTraceabilityModule(builder.Configuration);
builder.Services.AddDependenciesModule(builder.Configuration);
builder.Services.AddGovernanceModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);

// Authentication (Keycloak OIDC bearer, ADR-0004) + policy-based authorization (docs/10 matrix).
builder.Services.AddAcmpAuthentication(builder.Configuration);
builder.Services.AddAcmpAuthorization(builder.Configuration);

// One MediatR registration over the shared + module application assemblies.
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
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

// Enums on the wire as their string names (stable, localizable in the SPA; matches the read DTOs).
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// Problem Details error model (no leaking stack traces).
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Health checks: liveness (self) + readiness (SQL Server). The "api" check is trivially healthy when
// the app is serving the request — it backs the Administration → System Health "Application" tile (NR-08).
var connectionString = builder.Configuration.GetConnectionString("Acmp") ?? string.Empty;
builder.Services.AddHealthChecks()
    .AddCheck("api", () => HealthCheckResult.Healthy("Serving requests"), tags: new[] { "live" })
    .AddSqlServer(connectionString, name: "sqlserver", tags: new[] { "ready" });

// Action reminder/escalation knobs (docs/29 §3.4, W22) — bound from appsettings "ActionReminders".
builder.Services.Configure<ActionReminderOptions>(
    builder.Configuration.GetSection(ActionReminderOptions.SectionName));

// Background jobs — app-owned Hangfire on ACMP's OWN SQL (ADR-0014, CON-001). Its schema bootstraps its own
// tables; it never shares ACMP's domain tables and adds no external service. Skipped under the "Testing" host
// (the integration factory swaps SQL for EF-InMemory) so tests never open a real SQL connection.
var backgroundJobsEnabled = !builder.Environment.IsEnvironment("Testing")
    && !string.IsNullOrWhiteSpace(connectionString);
if (backgroundJobsEnabled)
{
    builder.Services.AddHangfire(cfg => cfg
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
        {
            SchemaName = "HangFire",
            PrepareSchemaIfNecessary = true,
        }));
    builder.Services.AddHangfireServer();
}

// OpenTelemetry traces/metrics over OTLP (Seq ingests OTLP). Endpoint from OTEL_* env vars.
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddOtlpExporter())
    .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddOtlpExporter());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

// Swagger in non-production only (OQ-019).
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/readyz", new HealthCheckOptions { Predicate = h => h.Tags.Contains("ready") });

app.MapMembershipEndpoints();
app.MapTopicEndpoints();
app.MapMeetingEndpoints();
app.MapMinutesEndpoints();
app.MapDecisionEndpoints();
app.MapVoteEndpoints();
app.MapActionEndpoints();
app.MapRiskEndpoints();
app.MapTraceabilityEndpoints();
app.MapDependencyEndpoints();
app.MapAdrEndpoints();
app.MapInvariantEndpoints();
app.MapNotificationEndpoints();
app.MapAdminEndpoints();

// Apply EF migrations on startup, retrying while SQL Server finishes accepting connections.
await MigrationRunner.MigrateAsync(app);

// Recurring action reminder/escalation sweep (AC-054/055). The job body just sends the MediatR command — all
// logic lives in the (unit-tested) SweepActionRemindersHandler; Hangfire only cron-triggers it.
if (backgroundJobsEnabled)
{
    var reminderOptions = builder.Configuration.GetSection(ActionReminderOptions.SectionName)
        .Get<ActionReminderOptions>() ?? new ActionReminderOptions();
    // Use the DI-resolved manager, NOT the static RecurringJob — the static one reads JobStorage.Current,
    // which isn't initialized until the Hangfire hosted server starts (after this point), so it would throw.
    app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<ISender>("action-reminders",
        sender => sender.Send(new SweepActionRemindersCommand(), CancellationToken.None),
        reminderOptions.SweepCron);
}

app.Run();

// Exposed for WebApplicationFactory-based integration tests in later phases.
public partial class Program { }
