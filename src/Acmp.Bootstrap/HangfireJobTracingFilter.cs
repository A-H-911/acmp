using System.Diagnostics;
using Acmp.Shared.Infrastructure.Observability;
using Hangfire.Server;

namespace Acmp.Bootstrap;

// DW-062 follow-on: an OTel span around every Hangfire job EXECUTION. NFR-043 only requires the DISPATCH
// span (its sentence is scoped to what happens inside an inbound HTTP request), so this is the other half —
// what the worker does after, in its own process, under its own trace.
//
// ⚠ WHY HAND-ROLLED. OpenTelemetry.Instrumentation.Hangfire does exactly this, and does it with the same
// IServerFilter mechanism — but it has never shipped a stable release (newest 1.17.0-beta.1), and a
// prerelease dependency in a production image is a decision the operator makes, not one a filter this small
// justifies. Roughly twenty lines against a package that would be pinned, scanned and Dependabot-managed.
//
// Lives in Acmp.Bootstrap rather than Acmp.Shared because it needs Hangfire types and Acmp.Shared
// deliberately carries no Hangfire reference; this is also where AddAcmpHangfireStorage registers it.
public sealed class HangfireJobTracingFilter : IServerFilter
{
    // ⚠ The Activity travels through PerformContext.Items, NOT a field on this class. Hangfire resolves a
    // filter ONCE and reuses that instance across every worker thread, so an instance field would be shared
    // state across concurrently executing jobs — each job would stop whichever activity happened to be
    // stored last. Items is per-invocation and is the mechanism Hangfire provides for exactly this.
    private const string ActivityKey = "acmp.otel.activity";

    public void OnPerforming(PerformingContext context)
    {
        var job = context.BackgroundJob.Job;
        var activity = AcmpTelemetry.StartJobExecution(
            job.Type.Name, job.Method.Name, context.BackgroundJob.Id);
        // Only store a real activity: nothing listening means nothing to stop, and a null in Items would
        // just have to be re-checked on the way out.
        if (activity is not null) context.Items[ActivityKey] = activity;
    }

    public void OnPerformed(PerformedContext context)
    {
        if (!context.Items.TryGetValue(ActivityKey, out var stored) || stored is not Activity activity) return;

        // A job that threw must not look like a job that succeeded. Hangfire hands the exception here rather
        // than letting it escape the filter, so this is the only place the span can learn the outcome.
        //
        // ⚠ UNWRAP FIRST. Hangfire wraps whatever the job threw in a JobPerformanceException carrying the
        // fixed message "An exception occurred during performance of the job." Recording that verbatim would
        // stamp EVERY failed job with the same type and the same useless message — the span would say a job
        // failed and never say why. Caught by a test that asserted the real message and got the wrapper's.
        if (context.Exception is not null)
        {
            var cause = context.Exception.InnerException ?? context.Exception;
            activity.SetStatus(ActivityStatusCode.Error, cause.Message);
            activity.SetTag("error.type", cause.GetType().Name);
        }

        activity.Dispose();
    }
}
