using System.Diagnostics;

namespace Acmp.Shared.Infrastructure.Observability;

// NFR-043 / DW-062: the ACMP-owned ActivitySource. NFR-043 asks that every inbound HTTP request produce a
// trace with spans for the HTTP handler, the DB query, Hangfire job DISPATCH, and outbound HTTP. Three of
// those come from off-the-shelf instrumentation (AspNetCore, SqlClient, Http). Job dispatch does not.
//
// ⚠ WHY THIS EXISTS RATHER THAN A PACKAGE. OpenTelemetry.Instrumentation.Hangfire has never shipped a
// stable release — the newest is 1.17.0-beta.1 — and it instruments job EXECUTION (a server filter) rather
// than the enqueue NFR-043 names. Taking a prerelease dependency into a production image to get the wrong
// half is a poor trade; an ActivitySource is in the BCL and costs nothing when nobody is listening.
//
// COST WHEN UNOBSERVED: StartActivity returns null if no listener has opted into the source, so an
// un-instrumented host (every unit test, and any deployment with tracing off) pays one null check.
public static class AcmpTelemetry
{
    /// The source name hosts must pass to AddSource(...) for these spans to be exported.
    public const string SourceName = "Acmp.Jobs";

    /// The one ActivitySource for app-emitted spans. Static by design: an ActivitySource is thread-safe and
    /// is meant to be long-lived — creating one per call silently drops spans, because listeners subscribe
    /// to sources at creation time.
    public static readonly ActivitySource Source = new(SourceName);

    /// <summary>
    /// Starts a span covering a Hangfire enqueue/schedule, or returns null when nothing is listening.
    /// </summary>
    /// <param name="operation">Enqueue or Schedule — the Hangfire client call being made.</param>
    /// <param name="jobType">The job type being dispatched, for the span's attributes.</param>
    /// <remarks>
    /// ActivityKind.Producer: this side hands work to a queue that another process consumes, which is the
    /// producer/consumer shape OTel's messaging conventions describe — not Internal, which would suggest the
    /// work happens here. The worker executes it in a different process entirely (ADR-0024).
    /// </remarks>
    public static Activity? StartJobDispatch(string operation, Type jobType)
    {
        var activity = Source.StartActivity($"hangfire.dispatch {operation}", ActivityKind.Producer);
        // Guarded, not unconditional: when a listener samples the activity out, StartActivity returns null
        // and every SetTag below would NRE.
        activity?.SetTag("messaging.system", "hangfire");
        activity?.SetTag("messaging.operation", operation);
        activity?.SetTag("acmp.job.type", jobType.Name);
        return activity;
    }

    /// <summary>
    /// Starts a span covering one Hangfire job EXECUTION, or returns null when nothing is listening.
    /// </summary>
    /// <remarks>
    /// The counterpart to <see cref="StartJobDispatch"/>: dispatch is what an inbound request does (NFR-043),
    /// execution is what the worker does later, in another process, under its own trace. ActivityKind.Consumer
    /// is the receiving half of the producer/consumer pair — a job is work taken off a queue, not a nested
    /// operation of whatever enqueued it.
    /// <para>
    /// ⚠ These two spans are NOT parented to each other. Hangfire serializes a job as type + method + arguments
    /// and carries no header the way a message broker does, so there is nowhere to put a W3C traceparent
    /// without changing the serialized job payload. Linking them would need trace context persisted alongside
    /// the job — deliberately out of scope here, and the reason the execution span stands alone.
    /// </para>
    /// </remarks>
    public static Activity? StartJobExecution(string jobType, string method, string jobId)
    {
        var activity = Source.StartActivity($"hangfire.execute {jobType}.{method}", ActivityKind.Consumer);
        activity?.SetTag("messaging.system", "hangfire");
        activity?.SetTag("messaging.operation", "process");
        activity?.SetTag("acmp.job.type", jobType);
        activity?.SetTag("acmp.job.method", method);
        activity?.SetTag("acmp.job.id", jobId);
        return activity;
    }
}
