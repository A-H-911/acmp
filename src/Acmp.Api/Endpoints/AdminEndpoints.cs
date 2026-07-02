using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Hangfire;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Acmp.Api.Endpoints;

// Administration system endpoints (NR-08). The System Health tab reads the live status of the ASP.NET
// health checks that are actually registered (api liveness + SQL Server in v1); the Job Monitor tab reads
// Hangfire's own monitoring API. Both are Admin-config gated (docs/10 §C -> Policies.AdminConfig). Honest by
// construction: health surfaces only what is truly monitored (every other service tile renders as
// "monitoring not configured"); jobs surfaces only what actually runs and reports Configured=false when
// Hangfire isn't wired (the "Testing" host / no connection string) rather than inventing a status. Uptime% /
// p95 are not collected on-prem in v1, so they are intentionally absent (recorded design deviation).
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").WithTags("Administration")
            .RequireAuthorization(Policies.AdminConfig);

        group.MapGet("/health", async (HealthCheckService health, CancellationToken ct) =>
        {
            var report = await health.CheckHealthAsync(ct);
            var entries = report.Entries
                .Select(e => new HealthEntryDto(
                    e.Key,
                    e.Value.Status.ToString(),
                    e.Value.Description,
                    Math.Round(e.Value.Duration.TotalMilliseconds, 1)))
                .ToArray();
            return Results.Ok(new SystemHealthDto(report.Status.ToString(), entries));
        });

        // Job Monitor (AC-056). JobStorage is resolved from DI as optional: it's only registered when
        // background jobs are enabled (Program.cs), so the "Testing" host / a stack with no connection
        // string has none -> Configured=false. The live GetMonitoringApi() -> JobsDto mapping lives in the
        // (unit-tested) JobsMonitorMapper; this endpoint is the thin, tolerant seam around it.
        group.MapGet("/jobs", (IServiceProvider services) =>
        {
            var storage = services.GetService<JobStorage>();
            return Results.Ok(storage is null
                ? JobsDto.NotConfigured
                : JobsMonitorMapper.Map(storage.GetMonitoringApi()));
        });

        // Retry a failed job (the design's Retry button). Not read-only -> audited (guardrail #5). Requeue
        // returns false when the job id is unknown or not in a re-queueable state -> 404.
        group.MapPost("/jobs/{id}/requeue", async (
            string id, IServiceProvider services, ICurrentUser user, IAuditSink audit, CancellationToken ct) =>
        {
            var client = services.GetService<IBackgroundJobClient>();
            if (client is null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Background jobs are not configured.");
            }

            var requeued = client.Requeue(id);
            await audit.EmitAsync("admin.job.requeued", user.UserId, new { JobId = id, Requeued = requeued }, ct);
            return requeued ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }

    public sealed record SystemHealthDto(string Status, IReadOnlyList<HealthEntryDto> Entries);

    public sealed record HealthEntryDto(string Name, string Status, string? Description, double DurationMs);
}
