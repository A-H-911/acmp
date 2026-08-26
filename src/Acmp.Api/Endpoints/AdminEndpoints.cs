using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Infrastructure.Audit;
using Acmp.Shared.Infrastructure.Configuration;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Acmp.Api.Endpoints;

// Administration system endpoints (NR-08). The System Health tab reads the live status of the ASP.NET
// health checks that are actually registered (api liveness + SQL Server in v1); the Job Monitor tab reads
// Hangfire's own monitoring API. Both are Admin-config gated (docs/domain/permission-role-matrix.md §C -> Policies.AdminConfig). Honest by
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

        /*
         * WBS-24.5 (DW-036 / FR-155, NFR-059, NFR-060; DEC-080 / SC-035) — retention configuration.
         * Already inside the AdminConfig-gated group, which AuthorizationRegistration maps to
         * Administrator ALONE; SEC-077 classifies a "retention/immutability config change" as a
         * privileged action, so no new policy was invented for it.
         */
        group.MapGet("/retention", async (ConfigurationDbContext db, CancellationToken ct) =>
        {
            var settings = await db.Settings
                .Where(s => s.Scope == RetentionScope)
                .OrderBy(s => s.Key)
                .Select(s => new RetentionSettingDto(s.Key, s.ValueJson))
                .ToListAsync(ct);
            // AutomaticPurgeEnabled is a CONSTANT, not a setting, and that is deliberate. No purge path
            // exists — SEC-089 places enforcement in Phase 2 — so a togglable "enable purge" would promise
            // something nothing implements, which is worse than the gap it papers over. Reported as a fact
            // so the surface can state the v1 posture instead of implying it.
            return Results.Ok(new RetentionPolicyDto(AutomaticPurgeEnabled: false, settings));
        });

        // Not read-only -> audited (guardrail #5, and SEC-077 specifically). Upsert: the key is identity.
        group.MapPut("/retention/{key}", async (
            string key, RetentionValueRequest body, ConfigurationDbContext db, AuditChangeBuffer buffer,
            IAuditSink audit, CancellationToken ct) =>
        {
            var normalized = (key ?? string.Empty).Trim().ToLowerInvariant();
            // Boundary validation. This endpoint owns the `retention.` namespace and nothing else: the
            // Configuration table is shared, so an unprefixed key here would let a retention screen write
            // any setting in the store.
            if (!normalized.StartsWith(RetentionKeyPrefix, StringComparison.Ordinal))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["key"] = [$"A retention key must begin with '{RetentionKeyPrefix}'."],
                });

            if (string.IsNullOrWhiteSpace(body?.ValueJson) || !IsJson(body.ValueJson))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["valueJson"] = ["The value must be well-formed JSON."],
                });

            var existing = await db.Settings.FirstOrDefaultAsync(s => s.Key == normalized, ct);
            var beforeJson = existing?.ValueJson;
            if (existing is null)
                db.Settings.Add(ConfigurationSetting.Create(normalized, body.ValueJson, RetentionScope));
            else
                existing.SetValue(body.ValueJson);
            await db.SaveChangesAsync(ct);

            /*
             * ⚠ THE BEFORE/AFTER IS SUPPLIED BY HAND, AND IT HAS TO BE. AuditCapture's interceptor observes
             * module SaveChanges for AuditableEntity; ConfigurationSetting is neither, by design — a config
             * row is not audit-STAMPED, it is audited by the command that changes it, because SEC-077 wants
             * the actor and the delta named. So the capture is added explicitly and the enriched sink drains
             * it. Emitting without this would produce an audit row with a null delta: present, and useless.
             */
            buffer.Add(new AuditChange(nameof(ConfigurationSetting), normalized, beforeJson, body.ValueJson));
            await audit.EmitEnrichedAsync("config.retention.set", nameof(ConfigurationSetting), normalized, ct: ct);
            return Results.NoContent();
        });

        return app;
    }

    /// <summary>Groups retention keys in the shared Configuration store (SEC-103's Scope column).</summary>
    private const string RetentionScope = "retention";

    /// <summary>Every key this endpoint may write. See the boundary check above for why it is enforced.</summary>
    private const string RetentionKeyPrefix = "retention.";

    private static bool IsJson(string value)
    {
        try
        {
            using var _ = System.Text.Json.JsonDocument.Parse(value);
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    public sealed record SystemHealthDto(string Status, IReadOnlyList<HealthEntryDto> Entries);

    public sealed record HealthEntryDto(string Name, string Status, string? Description, double DurationMs);

    /// <summary>The v1 retention posture plus whatever legal has configured — which in v1 is nothing.</summary>
    public sealed record RetentionPolicyDto(bool AutomaticPurgeEnabled, IReadOnlyList<RetentionSettingDto> Settings);

    public sealed record RetentionSettingDto(string Key, string ValueJson);

    public sealed record RetentionValueRequest(string ValueJson);
}
