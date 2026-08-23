using Acmp.Bootstrap;
using Acmp.Modules.Actions.Application.Reminders;
using Acmp.Modules.Membership.Application.Features.ExpireGuestAccess;
using Acmp.Modules.Topics.Application.Features.SweepTopicSla;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Observability;
using Hangfire;
using MediatR;
using OpenTelemetry.Trace;
using Serilog;

// Acmp.Worker — the dedicated background-job host (ADR-0024). The API enqueues; this process runs the Hangfire
// server that executes. Both hosts compose the SAME module graph via AddAcmpModules, so a job serialized by the
// API constructs correctly here.
var builder = Host.CreateApplicationBuilder(args);

// Docker secrets (docs/domain/deployment.md §3.3, ADR-0032): mirror the API — /run/secrets files become config
// keys (`__` -> `:`), added last, optional. The worker reads the same ConnectionStrings__Acmp / Minio__* secrets.
builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);

builder.Services.AddSerilog((services, config) => config
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    // C-PRIV-01/02: mask sensitive structured properties (emails/tokens/secrets/signed URLs) before any sink.
    .Enrich.With(new Acmp.Shared.Infrastructure.Observability.SensitiveDataMaskingEnricher()));

builder.Services.AddAcmpModules(builder.Configuration);
// ponytail: no SystemCurrentUser override — every worker job either opts out of the MediatR auth check
// (SweepActionRemindersCommand isn't an IAuthorizedRequest) or hardcodes its own "system:*" audit actor, and
// CurrentUserService is null-safe with no HttpContext. Add one only if a future job reads ICurrentUser.

// This host PROCESSES jobs: shared Hangfire storage (identical to the API client) + the SERVER. The API owns EF
// migrations; the worker only needs the Hangfire schema, which PrepareSchemaIfNecessary bootstraps. Gated on a
// connection string so the composition-root smoke test can build the graph without a real SQL server.
var connectionString = builder.Configuration.GetConnectionString("Acmp") ?? string.Empty;
var backgroundJobsEnabled = !string.IsNullOrWhiteSpace(connectionString);
if (backgroundJobsEnabled)
{
    // Prod runtime = least-priv acmp_svc (no DDL): the `--migrate-only` deploy step pre-provisions the HangFire
    // schema, so Hangfire:PrepareSchema=false here. Dev/e2e keeps the default true (ADR-0031/0032, deployment.md §5).
    builder.Services.AddAcmpHangfireStorage(connectionString,
        builder.Configuration.GetValue("Hangfire:PrepareSchema", true));
    builder.Services.AddHangfireServer();
}

// OpenTelemetry traces over OTLP (ADR-0014 / DW-062). This host had NONE: the API that ENQUEUES a job was
// traced while the process that EXECUTES it emitted nothing, so a job's own DB work was invisible and any
// span it did produce would have been orphaned. Endpoint from the same OTEL_* env vars the API reads.
//
// ⚠ NO AspNetCore instrumentation here — see the csproj note: that package would drag in a framework
// reference this slim base image does not carry. Nothing is lost; this host serves no HTTP.
// AcmpTelemetry.SourceName is registered because the Webex jobs this host runs dispatch further Hangfire
// work through the same seam the API uses, so the source has to be exported from both hosts or those spans
// exist in one process and vanish in the other.
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddSqlClientInstrumentation()
        .AddSource(AcmpTelemetry.SourceName)
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());

var host = builder.Build();

// Recurring action reminder/escalation sweep (AC-054/055). The body just sends the MediatR command — all logic
// lives in the unit-tested SweepActionRemindersHandler; Hangfire only cron-triggers it. IRecurringJobManager
// writes the schedule to storage without the server running, so registering right after Build() is safe.
if (backgroundJobsEnabled)
{
    var reminderOptions = builder.Configuration.GetSection(ActionReminderOptions.SectionName)
        .Get<ActionReminderOptions>() ?? new ActionReminderOptions();
    host.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<ISender>("action-reminders",
        sender => sender.Send(new SweepActionRemindersCommand(), CancellationToken.None),
        reminderOptions.SweepCron);

    // D-16 / C-INS-02 (ADR-0030): nightly audit + vote hash-chain integrity sweep. Off-peak (03:00); the
    // verifier logs a high-importance alert + a durable AuditEvent on any detected tampering.
    host.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<IIntegrityVerifier>("integrity-verify",
        verifier => verifier.RunAsync(CancellationToken.None),
        Cron.Daily(3));

    // AC-057: daily backlog SLA-breach sweep — notifies the Secretary roster when a topic exceeds its urgency
    // SLA (time-in-status). All logic in SweepTopicSlaHandler; idempotent via Topic.SlaNotifiedAt (reset on
    // transition). Daily matches AC-057's "badge updates daily".
    host.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<ISender>("topic-sla-sweep",
        sender => sender.Send(new SweepTopicSlaCommand(), CancellationToken.None),
        Cron.Daily());

    // FR-159 / AC-092: close a guest presenter's access once their window has passed — locally, and
    // in Keycloak so the login itself stops working.
    //
    // HOURLY, NOT DAILY, and that is not arbitrary: this is DEFENCE IN DEPTH behind ADR-0039's
    // per-request revalidation, which already refuses an expired guest on their very next request.
    // So the sweep never gates access; it only bounds how long a disabled-in-ACMP account can still
    // LOG IN to Keycloak. Daily would leave that window up to 24 hours for no benefit, and anything
    // finer would poll a table that is empty in the ordinary case.
    host.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<ISender>("guest-access-expiry",
        sender => sender.Send(new ExpireGuestAccessCommand(), CancellationToken.None),
        Cron.Hourly());
}

host.Run();
