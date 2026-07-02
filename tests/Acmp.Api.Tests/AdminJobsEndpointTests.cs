using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Hangfire;
using Hangfire.States;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Acmp.Api.Tests;

// Job Monitor endpoints (AC-056). Admin-config gated (Administrator only). Under the "Testing" host
// Hangfire is never wired, so JobStorage/IBackgroundJobClient are absent from DI: GET reports
// Configured=false and Requeue is 503. This deterministically covers the authz gate + the tolerant
// not-configured branch; the live Configured=true counts and real requeue+audit are E2E-verified on the
// real SQL stack (the P8c-1 lesson: the Testing host can't tell "correctly off" from "broken in prod").
public class AdminJobsEndpointTests
{
    private sealed record JobsResponse(bool Configured, CountsResponse Counts, List<JobRow> Jobs);
    private sealed record CountsResponse(long Succeeded, long Processing, long Scheduled, long Enqueued, long Failed);
    private sealed record JobRow(string Id, string Name, string Queue, string Status, bool CanRetry);

    private static HttpClient Client(AcmpWebApplicationFactory factory, string? roles, string sub = "kc-admin")
    {
        var client = factory.CreateClient();
        if (roles is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
            client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        }
        return client;
    }

    [Fact] // Administrator reads jobs; with Hangfire off the endpoint is honest (Configured=false, zero counts).
    public async Task Administrator_gets_not_configured_jobs_when_hangfire_is_off()
    {
        await using var factory = new AcmpWebApplicationFactory();

        var response = await Client(factory, "Administrator").GetAsync("/api/admin/jobs");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JobsResponse>();
        body.Should().NotBeNull();
        body!.Configured.Should().BeFalse();
        body.Counts.Should().BeEquivalentTo(new CountsResponse(0, 0, 0, 0, 0));
        body.Jobs.Should().BeEmpty();
    }

    [Fact] // docs/10: Admin.Config is Administrator-only — a Member is forbidden.
    public async Task Non_admin_is_forbidden_on_jobs()
    {
        await using var factory = new AcmpWebApplicationFactory();

        var response = await Client(factory, "Member", sub: "kc-member").GetAsync("/api/admin/jobs");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // No bearer → 401 before anything runs.
    public async Task Unauthenticated_is_unauthorized_on_jobs()
    {
        await using var factory = new AcmpWebApplicationFactory();

        var response = await Client(factory, roles: null).GetAsync("/api/admin/jobs");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // Requeue is Admin-only; with background jobs off it returns 503 (Service Unavailable), never 500.
    public async Task Requeue_is_service_unavailable_when_hangfire_is_off()
    {
        await using var factory = new AcmpWebApplicationFactory();

        var response = await Client(factory, "Administrator").PostAsync("/api/admin/jobs/job-1/requeue", null);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact] // A non-admin can't requeue.
    public async Task Non_admin_is_forbidden_on_requeue()
    {
        await using var factory = new AcmpWebApplicationFactory();

        var response = await Client(factory, "Member", sub: "kc-member")
            .PostAsync("/api/admin/jobs/job-1/requeue", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Hangfire wired (substituted, no server): covers the live Map + requeue branches the "Testing"
    //     host otherwise skips. NSubstitute stands in for JobStorage/IBackgroundJobClient — the real
    //     GetMonitoringApi()/Requeue wiring is still E2E-verified on the real SQL stack.

    private static HttpClient WiredAdminClient(
        AcmpWebApplicationFactory factory, JobStorage? storage, IBackgroundJobClient? jobClient)
    {
        var client = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            if (storage is not null) services.AddSingleton(storage);
            if (jobClient is not null) services.AddSingleton(jobClient);
        })).CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "Administrator");
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, "kc-admin");
        return client;
    }

    private static JobStorage StorageReturning(StatisticsDto stats)
    {
        var api = Substitute.For<IMonitoringApi>();
        api.GetStatistics().Returns(stats);
        api.FailedJobs(Arg.Any<int>(), Arg.Any<int>())
            .Returns(new JobList<FailedJobDto>(Array.Empty<KeyValuePair<string, FailedJobDto>>()));
        api.ProcessingJobs(Arg.Any<int>(), Arg.Any<int>())
            .Returns(new JobList<ProcessingJobDto>(Array.Empty<KeyValuePair<string, ProcessingJobDto>>()));
        api.ScheduledJobs(Arg.Any<int>(), Arg.Any<int>())
            .Returns(new JobList<ScheduledJobDto>(Array.Empty<KeyValuePair<string, ScheduledJobDto>>()));
        api.SucceededJobs(Arg.Any<int>(), Arg.Any<int>())
            .Returns(new JobList<SucceededJobDto>(Array.Empty<KeyValuePair<string, SucceededJobDto>>()));
        api.Queues().Returns(new List<QueueWithTopEnqueuedJobsDto>());

        var storage = Substitute.For<JobStorage>();
        storage.GetMonitoringApi().Returns(api);
        return storage;
    }

    [Fact] // With Hangfire wired, the endpoint projects live statistics and reports Configured=true.
    public async Task Administrator_gets_live_counts_when_hangfire_is_wired()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var storage = StorageReturning(new StatisticsDto { Succeeded = 42, Failed = 1 });

        var response = await WiredAdminClient(factory, storage, jobClient: null).GetAsync("/api/admin/jobs");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JobsResponse>();
        body!.Configured.Should().BeTrue();
        body.Counts.Succeeded.Should().Be(42);
        body.Counts.Failed.Should().Be(1);
    }

    [Fact] // A successful requeue (ChangeState true) → 204 and an audit event is emitted.
    public async Task Requeue_succeeds_returns_no_content()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var jobClient = Substitute.For<IBackgroundJobClient>();
        jobClient.ChangeState(Arg.Any<string>(), Arg.Any<IState>(), Arg.Any<string>()).Returns(true);

        var response = await WiredAdminClient(factory, storage: null, jobClient)
            .PostAsync("/api/admin/jobs/job-1/requeue", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        jobClient.Received().ChangeState("job-1", Arg.Any<IState>(), Arg.Any<string>());
    }

    [Fact] // An unknown / non-requeueable job (ChangeState false) → 404, not a 500.
    public async Task Requeue_unknown_job_returns_not_found()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var jobClient = Substitute.For<IBackgroundJobClient>(); // ChangeState defaults to false

        var response = await WiredAdminClient(factory, storage: null, jobClient)
            .PostAsync("/api/admin/jobs/ghost/requeue", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
