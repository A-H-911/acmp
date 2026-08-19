using System.Diagnostics;
using Acmp.Bootstrap;
using Acmp.Shared.Infrastructure.Observability;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.Server;
using Hangfire.Storage;
using NSubstitute;

namespace Acmp.Application.Tests.Bootstrap;

// DW-062 follow-on: the Hangfire job-EXECUTION span (NFR-043 itself only needs the DISPATCH span).
//
// ⚠ THESE DRIVE THE FILTER THROUGH HANGFIRE'S OWN CONTEXT TYPES RATHER THAN A RUNNING SERVER, and that was
// a deliberate retreat. The first version ran a REAL in-memory BackgroundJobServer, which is the stronger
// test because it also proves Hangfire INVOKES the filter. It passed alone and failed in the full suite:
// Hangfire's JobStorage.Current and GlobalJobFilters are process-GLOBAL, and a second BackgroundJobServer
// started in a process that has already run one does not reliably pick up work — so the test reported zero
// spans, which reads exactly like a broken filter and is not. A test that is green alone and red in the
// suite is worse than no test, so the server was dropped rather than the flake tolerated.
// ⚠ WHAT THAT COSTS, stated rather than hidden: these prove the filter's LOGIC, not its REGISTRATION.
// That Hangfire actually calls it is carried by the .UseFilter line in AddAcmpHangfireStorage and verified
// by inspection only — the same class of gap as the AddSource line in each host's Program.cs.
public class HangfireJobTracingFilterTests
{
    public sealed class SampleJob
    {
        public Task RunAsync() => Task.CompletedTask;
    }

    private static PerformContext Context()
    {
        var job = Job.FromExpression<SampleJob>(j => j.RunAsync());
        var backgroundJob = new BackgroundJob("job-42", job, DateTime.UtcNow);
        return new PerformContext(
            null, Substitute.For<IStorageConnection>(), backgroundJob, Substitute.For<IJobCancellationToken>());
    }

    /// Subscribes to the ACMP source and records on STOP — so anything recorded here also proves the filter
    /// disposed the activity. One left open never reaches an exporter.
    private static (List<Activity> Recorded, ActivityListener Listener) Listen()
    {
        var recorded = new List<Activity>();
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AcmpTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = recorded.Add,
        };
        ActivitySource.AddActivityListener(listener);
        return (recorded, listener);
    }

    [Fact]
    public void A_completed_job_gets_a_consumer_span_naming_the_job_method_and_id()
    {
        var (recorded, listener) = Listen();
        using var _ = listener;
        var filter = new HangfireJobTracingFilter();
        var context = Context();

        filter.OnPerforming(new PerformingContext(context));
        filter.OnPerformed(new PerformedContext(context, null, false, null));

        var span = recorded.Should().ContainSingle().Subject;
        span.OperationName.Should().Be("hangfire.execute SampleJob.RunAsync");
        // Consumer, not Producer: this end TAKES work off the queue. The dispatch span is the Producer half.
        span.Kind.Should().Be(ActivityKind.Consumer);
        span.GetTagItem("messaging.system").Should().Be("hangfire");
        span.GetTagItem("messaging.operation").Should().Be("process");
        span.GetTagItem("acmp.job.type").Should().Be(nameof(SampleJob));
        span.GetTagItem("acmp.job.method").Should().Be(nameof(SampleJob.RunAsync));
        span.GetTagItem("acmp.job.id").Should().Be("job-42");
        span.Status.Should().Be(ActivityStatusCode.Unset, "a job that succeeded must not be marked an error");
    }

    [Fact]
    public void A_failed_job_records_the_REAL_cause_not_Hangfire_s_wrapper()
    {
        var (recorded, listener) = Listen();
        using var _ = listener;
        var filter = new HangfireJobTracingFilter();
        var context = Context();
        // Hangfire never hands the filter the job's own exception: it wraps it in a JobPerformanceException
        // whose message is the same fixed sentence for every failure. Recording that verbatim would stamp
        // every failed job identically and the span would never say WHY. This shape is what caught it.
        var wrapped = new JobPerformanceException(
            "An exception occurred during performance of the job.", new InvalidOperationException("boom"));

        filter.OnPerforming(new PerformingContext(context));
        filter.OnPerformed(new PerformedContext(context, null, false, wrapped));

        var span = recorded.Should().ContainSingle().Subject;
        span.Status.Should().Be(ActivityStatusCode.Error);
        span.StatusDescription.Should().Be("boom");
        span.GetTagItem("error.type").Should().Be(nameof(InvalidOperationException));
    }

    [Fact]
    public void With_nobody_listening_no_activity_is_created_and_the_filter_still_completes()
    {
        // The production path whenever tracing is off. OnPerformed must not assume OnPerforming stored
        // anything — if it did, every job in an untraced host would throw inside the filter.
        var filter = new HangfireJobTracingFilter();
        var context = Context();

        filter.OnPerforming(new PerformingContext(context));
        var act = () => filter.OnPerformed(new PerformedContext(context, null, false, null));

        act.Should().NotThrow();
        Activity.Current.Should().BeNull();
    }
}
