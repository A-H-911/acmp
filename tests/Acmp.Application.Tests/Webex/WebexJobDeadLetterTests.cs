using FluentAssertions;
using Hangfire;
using Hangfire.InMemory;
using Hangfire.States;

namespace Acmp.Application.Tests.Webex;

// AC-068: a job that keeps throwing exhausts its AutomaticRetry cap and is dead-lettered — moved to the Failed
// state, visible in the admin job monitor (IMonitoringApi). Runs a REAL in-memory Hangfire server; retry delays
// are forced to 0 in this harness only (production keeps its default backoff) so the test is fast. This proves
// the infrastructure guarantee the Webex jobs lean on: a non-transient WebexApiException bubbles past their 429
// catch and dead-letters instead of looping. Serialized (DisableParallelization) — Hangfire uses a
// process-global JobStorage.Current.
[Collection(HangfireServerCollection.Name)]
public class WebexJobDeadLetterTests
{
    [AutomaticRetry(Attempts = 1, DelaysInSeconds = new[] { 0 })]
    public sealed class AlwaysFailsJob
    {
        // Stands in for a bubbled non-transient WebexApiException reaching Hangfire.
        public Task RunAsync() => throw new InvalidOperationException("boom — simulates a bubbled WebexApiException");
    }

    [Fact]
    public async Task A_job_that_keeps_failing_is_dead_lettered_after_the_retry_cap()
    {
        var storage = new InMemoryStorage();
        JobStorage.Current = storage;
        using var server = new BackgroundJobServer(
            new BackgroundJobServerOptions { SchedulePollingInterval = TimeSpan.FromMilliseconds(100), WorkerCount = 1 },
            storage);

        var jobId = BackgroundJob.Enqueue<AlwaysFailsJob>(j => j.RunAsync());
        var monitor = storage.GetMonitoringApi();

        var deadLettered = await PollAsync(() => monitor.FailedCount() > 0, TimeSpan.FromSeconds(25));

        deadLettered.Should().BeTrue("a job past its retry cap must land in the Failed state (dead-letter)");
        monitor.JobDetails(jobId).History.Should().Contain(h => h.StateName == FailedState.StateName);
    }

    private static async Task<bool> PollAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return true;
            await Task.Delay(150);
        }
        return condition();
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class HangfireServerCollection
{
    public const string Name = "hangfire-server";
}
