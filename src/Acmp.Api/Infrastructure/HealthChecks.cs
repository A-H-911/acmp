using System.Diagnostics.CodeAnalysis;
using Hangfire;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Acmp.Api.Infrastructure;

// Readiness sub-checks beyond SQL Server (NFR-045: app/DB/Hangfire/Seq/MinIO). MinIO + Hangfire are CRITICAL — a
// failure makes /readyz return 503 so the deploy smoke test / proxy pulls the instance. Seq is DEGRADED-only: losing
// the log sink must not take the API out of rotation. External-service plumbing, un-unit-assertable like
// MinioFileStore / AddAcmpHangfireStorage — [ExcludeFromCodeCoverage] by the same convention.
[ExcludeFromCodeCoverage]
public static class ReadinessChecks
{
    public static IServiceCollection AddAcmpReadinessChecks(this IServiceCollection services,
        IHealthChecksBuilder hc, IConfiguration configuration)
    {
        services.AddHttpClient("health");
        var ready = new[] { "ready" };

        // MinIO exposes its own liveness endpoint; derive the URL from the app's Minio config (override via
        // HealthChecks:MinioUrl). Critical — object storage backs attachments/recordings.
        var scheme = configuration.GetValue("Minio:Secure", false) ? "https" : "http";
        var minioUrl = configuration["HealthChecks:MinioUrl"]
            ?? $"{scheme}://{configuration["Minio:Endpoint"]}/minio/health/live";
        hc.Add(new HealthCheckRegistration("minio",
            sp => new HttpHealthCheck(sp.GetRequiredService<IHttpClientFactory>(), minioUrl, degradedOnFailure: false),
            HealthStatus.Unhealthy, ready));

        // Hangfire: the storage's monitoring API answers only when the schema + connection are healthy.
        hc.Add(new HealthCheckRegistration("hangfire",
            _ => new HangfireHealthCheck(), HealthStatus.Unhealthy, ready));

        // Seq: best-effort ping (degraded-only). Override via HealthChecks:SeqUrl.
        var seqUrl = (configuration["HealthChecks:SeqUrl"] ?? "http://seq:5341").TrimEnd('/') + "/health";
        hc.Add(new HealthCheckRegistration("seq",
            sp => new HttpHealthCheck(sp.GetRequiredService<IHttpClientFactory>(), seqUrl, degradedOnFailure: true),
            HealthStatus.Degraded, ready));

        return services;
    }
}

[ExcludeFromCodeCoverage]
internal sealed class HttpHealthCheck(IHttpClientFactory factory, string url, bool degradedOnFailure) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = factory.CreateClient("health");
            client.Timeout = TimeSpan.FromSeconds(3);
            using var response = await client.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : Fail($"{url} -> HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }

        HealthCheckResult Fail(string message) =>
            degradedOnFailure ? HealthCheckResult.Degraded(message) : HealthCheckResult.Unhealthy(message);
    }
}

[ExcludeFromCodeCoverage]
internal sealed class HangfireHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = JobStorage.Current.GetMonitoringApi().GetStatistics();
            return Task.FromResult(HealthCheckResult.Healthy($"servers={stats.Servers}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(ex.Message));
        }
    }
}
