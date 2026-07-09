using System.Linq.Expressions;

namespace Acmp.Modules.Integrations.Webex;

// A thin seam over Hangfire's IBackgroundJobClient so jobs are testable without a running Hangfire server
// (Hangfire is off under the Testing host and its static API throws pre-server-start). Generic over the job
// type so it schedules both the card-send job and the webhook-processing job. Tests register an inline fake;
// production wires HangfireWebexJobScheduler.
public interface IWebexJobScheduler
{
    void Enqueue<TJob>(Expression<Func<TJob, Task>> methodCall) where TJob : class;

    void Schedule<TJob>(Expression<Func<TJob, Task>> methodCall, TimeSpan delay) where TJob : class;
}
