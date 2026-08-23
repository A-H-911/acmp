using System.Linq.Expressions;
using Acmp.Shared.Infrastructure.Observability;
using Hangfire;

namespace Acmp.Modules.Integrations.Webex;

// Production IWebexJobScheduler: enqueues/schedules on the app-owned Hangfire (ADR-0014). The API resolves
// IBackgroundJobClient from the Hangfire client (no server); the worker container runs the server that
// actually executes the jobs.
//
// NFR-043 / DW-062: this is the Hangfire job-DISPATCH span the requirement names. It belongs here rather
// than at the call sites because this seam is the single place an ACMP request hands work to Hangfire — one
// span site instead of one per caller, and a new caller is instrumented by construction. The span closes at
// the end of the enqueue, which is the operation being measured: how long handing the job over took, not how
// long the job later runs (that happens in another process — ADR-0024 — under its own trace).
public sealed class HangfireWebexJobScheduler : IWebexJobScheduler
{
    private readonly IBackgroundJobClient _jobs;

    public HangfireWebexJobScheduler(IBackgroundJobClient jobs) => _jobs = jobs;

    public void Enqueue<TJob>(Expression<Func<TJob, Task>> methodCall) where TJob : class
    {
        using var activity = AcmpTelemetry.StartJobDispatch("Enqueue", typeof(TJob));
        _jobs.Enqueue(methodCall);
    }

    public void Schedule<TJob>(Expression<Func<TJob, Task>> methodCall, TimeSpan delay) where TJob : class
    {
        using var activity = AcmpTelemetry.StartJobDispatch("Schedule", typeof(TJob));
        activity?.SetTag("acmp.job.delay_seconds", delay.TotalSeconds);
        _jobs.Schedule(methodCall, delay);
    }
}
