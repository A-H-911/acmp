using System.Linq.Expressions;
using Hangfire;

namespace Acmp.Modules.Integrations.Webex;

// Production IWebexJobScheduler: enqueues/schedules on the app-owned Hangfire (ADR-0014). The API resolves
// IBackgroundJobClient from the Hangfire client (no server); the worker container runs the server that
// actually executes the jobs.
public sealed class HangfireWebexJobScheduler : IWebexJobScheduler
{
    private readonly IBackgroundJobClient _jobs;

    public HangfireWebexJobScheduler(IBackgroundJobClient jobs) => _jobs = jobs;

    public void Enqueue<TJob>(Expression<Func<TJob, Task>> methodCall) where TJob : class =>
        _jobs.Enqueue(methodCall);

    public void Schedule<TJob>(Expression<Func<TJob, Task>> methodCall, TimeSpan delay) where TJob : class =>
        _jobs.Schedule(methodCall, delay);
}
