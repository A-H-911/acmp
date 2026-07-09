using System.Linq.Expressions;
using Acmp.Modules.Integrations.Webex;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using NSubstitute;

namespace Acmp.Application.Tests.Webex;

// The production scheduler simply delegates to the app-owned Hangfire IBackgroundJobClient (ADR-0014). The
// Enqueue/Schedule extensions route through IBackgroundJobClient.Create with an Enqueued/Scheduled state.
public class HangfireWebexJobSchedulerTests
{
    private static readonly Expression<Func<WebexSendJob, Task>> Call =
        j => j.SendSpaceMessageAsync("{}", CancellationToken.None);

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
}
