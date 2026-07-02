using System.Reflection;
using Acmp.Api.Endpoints;
using FluentAssertions;
using Hangfire.Common;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using NSubstitute;

namespace Acmp.Api.Tests;

// Pure projection of Hangfire's IMonitoringApi -> JobsDto (AC-056). Mocking the interface lets every
// mapping branch (each state, null-job, ordering, duration, counts) run deterministically with no
// Hangfire server — the exact coverage the "Testing" host can't give the live endpoint.
public class JobsMonitorMapperTests
{
    private static readonly MethodInfo SampleMethod =
        typeof(JobsMonitorMapperTests).GetMethod(nameof(Sample))!;

#pragma warning disable xUnit1013 // Hangfire's Job ctor requires a public method; this is a fixture, not a test.
    public static void Sample() { }
#pragma warning restore xUnit1013

    private static Job DefaultJob() => new(SampleMethod);
    private static Job QueuedJob(string queue) =>
        new(typeof(JobsMonitorMapperTests), SampleMethod, Array.Empty<object>(), queue);

    private static JobList<T> ListOf<T>(params (string Id, T Dto)[] items) =>
        new(items.Select(i => new KeyValuePair<string, T>(i.Id, i.Dto)));

    // A substitute whose enumerated methods all return empty by default, so a test only sets what it cares about.
    private static IMonitoringApi EmptyApi(StatisticsDto? stats = null)
    {
        var api = Substitute.For<IMonitoringApi>();
        api.GetStatistics().Returns(stats ?? new StatisticsDto());
        api.FailedJobs(Arg.Any<int>(), Arg.Any<int>()).Returns(ListOf<FailedJobDto>());
        api.ProcessingJobs(Arg.Any<int>(), Arg.Any<int>()).Returns(ListOf<ProcessingJobDto>());
        api.ScheduledJobs(Arg.Any<int>(), Arg.Any<int>()).Returns(ListOf<ScheduledJobDto>());
        api.SucceededJobs(Arg.Any<int>(), Arg.Any<int>()).Returns(ListOf<SucceededJobDto>());
        api.Queues().Returns(new List<QueueWithTopEnqueuedJobsDto>());
        return api;
    }

    [Fact] // The 5 tiles come straight off GetStatistics(), and a wired storage always reports Configured=true.
    public void Map_projects_statistics_into_counts()
    {
        var api = EmptyApi(new StatisticsDto
        {
            Succeeded = 1284,
            Processing = 2,
            Scheduled = 9,
            Enqueued = 12,
            Failed = 1,
        });

        var dto = JobsMonitorMapper.Map(api);

        dto.Configured.Should().BeTrue();
        dto.Counts.Should().BeEquivalentTo(new JobCountsDto(1284, 2, 9, 12, 1));
        dto.Jobs.Should().BeEmpty();
    }

    [Fact] // Only failed rows are retryable; every state maps to its designed status string.
    public void Map_marks_only_failed_rows_retryable()
    {
        var api = EmptyApi();
        api.FailedJobs(Arg.Any<int>(), Arg.Any<int>())
            .Returns(ListOf(("f1", new FailedJobDto { Job = DefaultJob(), FailedAt = DateTime.UtcNow })));
        api.ProcessingJobs(Arg.Any<int>(), Arg.Any<int>())
            .Returns(ListOf(("p1", new ProcessingJobDto { Job = DefaultJob(), StartedAt = DateTime.UtcNow })));
        api.ScheduledJobs(Arg.Any<int>(), Arg.Any<int>())
            .Returns(ListOf(("sc1", new ScheduledJobDto { Job = DefaultJob(), EnqueueAt = DateTime.UtcNow })));

        var dto = JobsMonitorMapper.Map(api);

        dto.Jobs.Should().ContainSingle(j => j.Status == JobStatuses.Failed && j.CanRetry);
        dto.Jobs.Should().ContainSingle(j => j.Status == JobStatuses.Processing && !j.CanRetry);
        dto.Jobs.Should().ContainSingle(j => j.Status == JobStatuses.Scheduled && !j.CanRetry);
    }

    [Fact] // Name = method name, queue = the job's own queue; a null (undeserializable) job degrades honestly.
    public void Map_reads_name_and_queue_from_job_and_handles_null()
    {
        var api = EmptyApi();
        api.FailedJobs(Arg.Any<int>(), Arg.Any<int>()).Returns(ListOf(
            ("named", new FailedJobDto { Job = QueuedJob("render"), FailedAt = DateTime.UtcNow }),
            ("broken", new FailedJobDto { Job = null, FailedAt = DateTime.UtcNow })));

        var dto = JobsMonitorMapper.Map(api);

        var named = dto.Jobs.Single(j => j.Id == "named");
        named.Name.Should().Be(nameof(Sample));
        named.Queue.Should().Be("render");

        var broken = dto.Jobs.Single(j => j.Id == "broken");
        broken.Name.Should().Be("(unknown)");
        broken.Queue.Should().Be("default"); // no job -> default queue, never blank
    }

    [Fact] // Enqueued rows are read off Queues().FirstJobs, not a separate query.
    public void Map_reads_enqueued_rows_from_queue_first_jobs()
    {
        var api = EmptyApi();
        api.Queues().Returns(new List<QueueWithTopEnqueuedJobsDto>
        {
            new()
            {
                Name = "default",
                FirstJobs = ListOf(("e1", new EnqueuedJobDto { Job = DefaultJob(), EnqueuedAt = DateTime.UtcNow })),
            },
        });

        var dto = JobsMonitorMapper.Map(api);

        dto.Jobs.Should().ContainSingle(j => j.Id == "e1" && j.Status == JobStatuses.Enqueued && !j.CanRetry);
    }

    [Fact] // Succeeded duration comes from TotalDuration (ms); timestamps are surfaced as UTC instants.
    public void Map_carries_succeeded_duration_and_utc_timestamp()
    {
        var when = new DateTime(2026, 7, 2, 6, 0, 0, DateTimeKind.Utc);
        var api = EmptyApi();
        api.SucceededJobs(Arg.Any<int>(), Arg.Any<int>()).Returns(ListOf(
            ("s1", new SucceededJobDto { Job = DefaultJob(), SucceededAt = when, TotalDuration = 1200 })));

        var row = JobsMonitorMapper.Map(api).Jobs.Single();

        row.DurationMs.Should().Be(1200);
        row.Timestamp.Should().Be(new DateTimeOffset(when));
        row.Timestamp!.Value.Offset.Should().Be(TimeSpan.Zero); // interpreted as UTC, not local
    }

    [Fact] // Recent list is newest-first across all states.
    public void Map_orders_recent_jobs_newest_first()
    {
        var old = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var recent = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc);
        var api = EmptyApi();
        api.FailedJobs(Arg.Any<int>(), Arg.Any<int>())
            .Returns(ListOf(("older", new FailedJobDto { Job = DefaultJob(), FailedAt = old })));
        api.SucceededJobs(Arg.Any<int>(), Arg.Any<int>())
            .Returns(ListOf(("newer", new SucceededJobDto { Job = DefaultJob(), SucceededAt = recent })));

        var ids = JobsMonitorMapper.Map(api).Jobs.Select(j => j.Id).ToArray();

        ids.Should().Equal("newer", "older");
    }
}
