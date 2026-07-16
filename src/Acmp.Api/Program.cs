using Acmp.Api.Endpoints;
using Acmp.Api.Infrastructure;
using Acmp.Api.Infrastructure.Authentication;
using Acmp.Bootstrap;
using Acmp.Shared.Authorization;
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

// Shared kernel + all modules + Webex adapter + MediatR, composed identically to the worker via the shared
// composition root (Acmp.Bootstrap, ADR-0024) so both hosts resolve the same service graph.
builder.Services.AddAcmpModules(builder.Configuration);

// Authentication (Keycloak OIDC bearer, ADR-0004) + policy-based authorization (docs/domain/permission-role-matrix.md matrix).
builder.Services.AddAcmpAuthentication(builder.Configuration);
builder.Services.AddAcmpAuthorization(builder.Configuration);

// P16-B4: proportional rate limiting (C-API-03) + read-only-FS-safe DataProtection key-ring (C-CON-003).
builder.Services.AddAcmpRateLimiting(builder.Configuration);
builder.Services.AddAcmpDataProtection(builder.Configuration);

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

// Background jobs — app-owned Hangfire on ACMP's OWN SQL (ADR-0014, CON-001), ENQUEUE-ONLY here. The API
// registers the Hangfire CLIENT (IBackgroundJobClient); the SERVER that processes jobs runs in the dedicated
// Acmp.Worker container (ADR-0024). Skipped under the "Testing" host (the integration factory swaps SQL for
// EF-InMemory) so tests never open a real SQL connection.
var backgroundJobsEnabled = !builder.Environment.IsEnvironment("Testing")
    && !string.IsNullOrWhiteSpace(connectionString);
if (backgroundJobsEnabled)
    builder.Services.AddAcmpHangfireStorage(connectionString);

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

// After auth so the rate-limiter can partition by the caller's `sub` (see HardeningExtensions).
app.UseRateLimiter();

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
app.MapResearchEndpoints(); // P15a: research missions + findings + recommendations
app.MapKnowledgeEndpoints(); // P15d: knowledge documents (versioned wiki)
app.MapSearchEndpoints(); // P15f: global search (FR-143/144/145) — fan-out over module ISearchProviders
app.MapNotificationEndpoints();
app.MapAdminEndpoints();
app.MapAuditEndpoints(); // AC-017/019/020: Auditor read + on-demand chain-verify (read-only)
app.MapWebexEndpoints(); // P13: anonymous, HMAC-authenticated inbound Webex webhook

// The API OWNS schema migrations (both hosts share one SQL DB; a single migrator avoids a two-host race —
// the worker waits for the schema). Retries while SQL Server finishes accepting connections.
await MigrationRunner.MigrateAsync(app);

// The recurring action-reminder sweep (AC-054/055) is REGISTERED AND RUN by the Acmp.Worker container
// (ADR-0024), not here — the API no longer hosts a Hangfire server.

app.Run();

// Exposed for WebApplicationFactory-based integration tests in later phases.
public partial class Program { }
