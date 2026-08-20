using System.Diagnostics;
using System.Linq.Expressions;
using Acmp.Modules.Integrations.Webex;
using Acmp.Shared.Infrastructure.Observability;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using NSubstitute;

namespace Acmp.Application.Tests.Webex;

// The production scheduler simply delegates to the app-owned Hangfire IBackgroundJobClient (ADR-0014). The
// Enqueue/Schedule extensions route through IBackgroundJobClient.Create with an Enqueued/Scheduled state.
//
// The span tests below cover NFR-043's Hangfire job-dispatch clause (DW-062). This seam is the only place an
// ACMP request hands work to Hangfire, so it is the only place a dispatch span can come from.
// ⚠ SERIALISED (DEF-097). Listen() registers a PROCESS-GLOBAL ActivityListener on the ACMP source, and so
// does HangfireJobTracingFilterTests. Run in parallel, each records the other's span and ContainSingle()
// fails - observed as this class recording "hangfire.execute SampleJob.RunAsync". AcmpTelemetry has exactly
// two StartActivity sites and exactly two test classes listen, so serialising both closes the leak.
[Collection(HangfireServerCollection.Name)]
public class HangfireWebexJobSchedulerTests
{
    private static readonly Expression<Func<WebexSendJob, Task>> Call =
        j => j.SendSpaceMessageAsync("{}", CancellationToken.None);

    /// Subscribes to the ACMP source and records every activity it starts. Without a listener that both
    /// matches the source AND samples AllData, ActivitySource.StartActivity returns null and NOTHING is
    /// recorded — which is exactly the production no-listener path asserted separately below.
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
    public void Enqueue_creates_a_job_in_the_enqueued_state()
    {
        var jobs = Substitute.For<IBackgroundJobClient>();

        new HangfireWebexJobScheduler(jobs).Enqueue(Call);

        jobs.Received(1).Create(Arg.Any<Job>(), Arg.Is<IState>(s => s is EnqueuedState));
    }

    [Fact]
    public void Schedule_creates_a_job_in_the_scheduled_state()
    {
        var jobs = Substitute.For<IBackgroundJobClient>();

        new HangfireWebexJobScheduler(jobs).Schedule(Call, TimeSpan.FromSeconds(30));

        jobs.Received(1).Create(Arg.Any<Job>(), Arg.Is<IState>(s => s is ScheduledState));
    }

    [Fact]
    public void Enqueue_emits_a_producer_dispatch_span_naming_the_job_type()
    {
        var (recorded, listener) = Listen();
        using var _ = listener;

        new HangfireWebexJobScheduler(Substitute.For<IBackgroundJobClient>()).Enqueue(Call);

        var span = recorded.Should().ContainSingle().Subject;
        span.OperationName.Should().Be("hangfire.dispatch Enqueue");
        // Producer, not Internal: the work is executed by the worker process, not here (ADR-0024).
        span.Kind.Should().Be(ActivityKind.Producer);
        span.GetTagItem("messaging.system").Should().Be("hangfire");
        span.GetTagItem("messaging.operation").Should().Be("Enqueue");
        span.GetTagItem("acmp.job.type").Should().Be(nameof(WebexSendJob));
        // The span must CLOSE — `using var` in the seam. A span left open never reaches the exporter.
        span.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void Schedule_emits_a_dispatch_span_carrying_the_delay()
    {
        var (recorded, listener) = Listen();
        using var _ = listener;

        new HangfireWebexJobScheduler(Substitute.For<IBackgroundJobClient>())
            .Schedule(Call, TimeSpan.FromSeconds(30));

        var span = recorded.Should().ContainSingle().Subject;
        span.OperationName.Should().Be("hangfire.dispatch Schedule");
        span.GetTagItem("messaging.operation").Should().Be("Schedule");
        span.GetTagItem("acmp.job.delay_seconds").Should().Be(30d);
    }

    [Fact]
    public void Dispatching_with_nobody_listening_costs_no_activity_and_still_enqueues()
    {
        // No listener registered: StartActivity returns null, every SetTag is a no-op, and the job must
        // still be handed to Hangfire. This is the production path whenever tracing is off, so if the seam
        // ever depended on a non-null activity it would break dispatch itself rather than just telemetry.
        var jobs = Substitute.For<IBackgroundJobClient>();

        new HangfireWebexJobScheduler(jobs).Enqueue(Call);

        AcmpTelemetry.StartJobDispatch("Enqueue", typeof(WebexSendJob)).Should().BeNull();
        jobs.Received(1).Create(Arg.Any<Job>(), Arg.Is<IState>(s => s is EnqueuedState));
    }
}
