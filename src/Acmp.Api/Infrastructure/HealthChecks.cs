using System.Diagnostics.CodeAnalysis;
using Acmp.Shared.Application.Abstractions;
using Hangfire;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

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

        /*
         * Object storage readiness (DEF-078, DEC-056 d1). Critical — object storage backs attachments/recordings.
         *
         * ⚠ THIS USED TO GET `{scheme}://{Minio:Endpoint}/minio/health/live`, AND IT WAS MEASURED RETURNING 503
         * ON PRODUCTION FOR THREE DAYS. That path is a MinIO-daemon endpoint. The cloud stack points
         * Minio__Endpoint at s3.us-east-1.amazonaws.com, which does not serve it and answers 301; HttpHealthCheck
         * treats any non-success as a failure and this registration passed degradedOnFailure:false, so /readyz
         * was Unhealthy on the environment it runs in. The bug was not the URL — it was probing a DAEMON
         * LIVENESS PATH to answer a question about OUR storage.
         *
         * BucketExistsAsync is environment-aware BY CONSTRUCTION rather than by a branch or a config flag, which
         * is what DEC-047 asked for: the same call works against MinIO and S3 because it goes through the one
         * Minio SDK client both already use (ADR-0035). It also proves the thing that actually matters — that
         * THIS app can reach ITS bucket with ITS credentials — instead of that some daemon is alive. The IAM
         * policy grants s3:ListBucket, which is what makes HeadBucket succeed against S3 (see MinioFileStore's
         * EnsureBucketAsync note); a policy tightened below that will fail this check LOUDLY, which is correct.
         */
        hc.Add(new HealthCheckRegistration("objectstore",
            sp => new ObjectStoreHealthCheck(
                sp.GetRequiredService<IMinioClient>(),
                sp.GetRequiredService<IOptions<StorageOptions>>()),
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

/*
 * Object-store readiness: can THIS app reach ITS buckets with ITS credentials (DEF-078, DEC-056 d1).
 *
 * ⚠ DELIBERATELY NOT [ExcludeFromCodeCoverage], unlike the HTTP plumbing around it. That attribute was
 * defensible on a health check that could only be exercised by standing up a real MinIO; the whole point of
 * moving to an SDK call is that IMinioClient is an interface, so every branch here is assertable with a
 * substitute. An untested health check is how DEF-078 shipped: nothing failed when the check became
 * incapable of passing, because nothing was watching the check itself.
 */
internal sealed class ObjectStoreHealthCheck(IMinioClient client, IOptions<StorageOptions> storage) : IHealthCheck
{
    // The SDK call has no timeout of its own. The HTTP probe it replaces capped itself at 3s, and losing that
    // would let a hung endpoint stall /readyz instead of failing it — a readiness endpoint that hangs is worse
    // than one that says no, because callers time out at their own layer with no reason attached.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Both names, deduped: on-prem keeps the historical recordings/attachments split while the cloud stack
        // points both at one per-environment bucket, so this is two probes on-prem and exactly one on cloud.
        var buckets = new[] { storage.Value.RecordingsBucket, storage.Value.AttachmentsBucket }
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // Misconfiguration is Unhealthy, never a vacuous pass: a check with nothing to probe that reports
        // Healthy is the failure mode this project keeps recording, and it is exactly how a control goes green
        // while proving nothing.
        if (buckets.Length == 0)
            return HealthCheckResult.Unhealthy("no object-storage buckets configured (Storage:RecordingsBucket / Storage:AttachmentsBucket)");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(Timeout);

        foreach (var bucket in buckets)
        {
            try
            {
                // Name the bucket in every failure — the "tell" half. A bare "Unhealthy" sent someone to read
                // source for three days; the operator must learn WHICH bucket and WHY from the response alone.
                if (!await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), cts.Token))
                    return HealthCheckResult.Unhealthy($"object-storage bucket '{bucket}' does not exist or is not visible to this identity");
            }
            // ⚠ THIS CLAUSE MUST COME FIRST AND THE ORDER IS LOAD-BEARING. A caller-initiated cancellation
            // (shutdown, client disconnect) is not a storage fault, and rethrowing it keeps a routine shutdown
            // out of the health record. Without this, the generic Exception clause below swallows it and
            // reports an object-store outage that never happened — which is what the first draft of this
            // method did, and a test caught it rather than a reviewer.
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            // Our own CancelAfter fired: the store never answered. That IS a storage fault, and it is reported
            // as a timeout rather than as a generic error so the operator can tell "slow/hung" from "refused".
            catch (OperationCanceledException)
            {
                return HealthCheckResult.Unhealthy($"object-storage probe for bucket '{bucket}' timed out after {Timeout.TotalSeconds:0}s");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy($"object-storage bucket '{bucket}' unreachable: {ex.Message}");
            }
        }

        return HealthCheckResult.Healthy($"buckets reachable: {string.Join(", ", buckets)}");
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
