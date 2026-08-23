using Acmp.Api.Infrastructure;
using FluentAssertions;
using Hangfire;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Acmp.Api.Tests;

/*
 * DEF-082. This check read the STATIC JobStorage.Current, which is only assigned when a Hangfire SERVER
 * starts. The API is deliberately enqueue-only (ADR-0024), so the static was never set and the check reported
 * "Current JobStorage instance has not been initialized yet" on every request, in every environment, from the
 * day it was written. It was [ExcludeFromCodeCoverage] with no tests, and nothing consumed /readyz - the
 * container healthcheck probed /healthz, which evaluates nothing (DEF-079) - so a check that failed 100% of
 * the time was invisible for its entire life. One test with a fake storage would have caught it.
 */
public sealed class HangfireHealthCheckTests
{
    private static HealthCheckContext Context() => new();

    private static JobStorage StorageReturning(StatisticsDto stats)
    {
        var monitoring = Substitute.For<IMonitoringApi>();
        monitoring.GetStatistics().Returns(stats);
        var storage = Substitute.For<JobStorage>();
        storage.GetMonitoringApi().Returns(monitoring);
        return storage;
    }

    [Fact]
    public async Task Reports_healthy_when_the_storage_answers()
    {
        var check = new HangfireHealthCheck(StorageReturning(new StatisticsDto { Servers = 1, Enqueued = 3 }));

        var result = await check.CheckHealthAsync(Context());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    /*
     * THE REGRESSION GUARD THAT MATTERS. The API enqueues and the WORKER processes, so zero registered servers
     * is the normal, correct state for this process. Treating it as a failure would recreate the same
     * always-red check by a different route.
     */
    [Fact]
    public async Task Reports_healthy_when_no_worker_server_is_registered()
    {
        var check = new HangfireHealthCheck(StorageReturning(new StatisticsDto { Servers = 0, Enqueued = 0 }));

        var result = await check.CheckHealthAsync(Context());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Reports_unhealthy_and_says_why_when_the_storage_throws()
    {
        var storage = Substitute.For<JobStorage>();
        storage.GetMonitoringApi().Throws(new InvalidOperationException("login failed for user acmp_svc"));

        var result = await new HangfireHealthCheck(storage).CheckHealthAsync(Context());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("hangfire storage unreachable");
        result.Description.Should().Contain("login failed for user acmp_svc");
    }
}
