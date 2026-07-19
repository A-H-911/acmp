using Acmp.Bootstrap;
using Acmp.Modules.Actions.Application.Reminders;
using Acmp.Shared.Application.Abstractions;
using Hangfire;
using MediatR;
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
    builder.Services.AddAcmpHangfireStorage(connectionString);
    builder.Services.AddHangfireServer();
}

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
}

host.Run();
