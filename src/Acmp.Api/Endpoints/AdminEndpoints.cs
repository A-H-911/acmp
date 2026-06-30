using Acmp.Shared.Authorization;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Acmp.Api.Endpoints;

// Administration system endpoints (NR-08). The System Health tab reads the live status of the
// ASP.NET health checks that are actually registered (api liveness + SQL Server in v1). Admin-config
// gated (docs/10 §C → Policies.AdminConfig). Honest by construction: it surfaces only what is truly
// monitored — the SPA renders every other service tile (MinIO/Seq/Hangfire/Webex) as "monitoring not
// configured" rather than inventing a status. Uptime% / p95 are not collected on-prem in v1, so they
// are intentionally absent (recorded design deviation — see the P4-reconcile findings table).
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

        return app;
    }

    public sealed record SystemHealthDto(string Status, IReadOnlyList<HealthEntryDto> Entries);

    public sealed record HealthEntryDto(string Name, string Status, string? Description, double DurationMs);
}
