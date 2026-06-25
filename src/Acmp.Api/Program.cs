using Acmp.Api.Endpoints;
using Acmp.Api.Infrastructure;
using Acmp.Modules.Membership.Application;
using Acmp.Modules.Membership.Infrastructure;
using Acmp.Shared;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Serilog -> console + self-hosted Seq (ADR-0014). Fully configuration-driven.
builder.Host.UseSerilog((context, services, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// Shared kernel (clock, current-user, file store, MediatR behaviors) + modules.
builder.Services.AddSharedKernel(builder.Configuration);
builder.Services.AddMembershipModule(builder.Configuration);

// One MediatR registration over the shared + module application assemblies.
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
    typeof(SharedKernelExtensions).Assembly,
    MembershipApplicationExtensions.Assembly));

// Problem Details error model (no leaking stack traces).
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Health checks: liveness (self) + readiness (SQL Server).
var connectionString = builder.Configuration.GetConnectionString("Acmp") ?? string.Empty;
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "sqlserver", tags: new[] { "ready" });

// OpenTelemetry traces/metrics over OTLP (Seq ingests OTLP). Endpoint from OTEL_* env vars.
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddOtlpExporter())
    .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddOtlpExporter());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

// Swagger in non-production only (OQ-019).
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/readyz", new HealthCheckOptions { Predicate = h => h.Tags.Contains("ready") });

app.MapMembershipEndpoints();

// Apply EF migrations on startup, retrying while SQL Server finishes accepting connections.
await MigrationRunner.MigrateAsync(app);

app.Run();

// Exposed for WebApplicationFactory-based integration tests in later phases.
public partial class Program { }
