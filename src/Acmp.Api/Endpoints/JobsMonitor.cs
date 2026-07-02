using Hangfire.Common;
using Hangfire.Storage;

namespace Acmp.Api.Endpoints;

// Job Monitor projection (AC-056; mirrors "ACMP Administration.dc.html" isJobs). A PURE mapper from
// Hangfire's read-side IMonitoringApi into the SPA's shape — 5 stat tiles + a recent-jobs table. It is
// deliberately a plain static function over the interface (no Hangfire *server* needed) so it is fully
// unit-testable against a mocked IMonitoringApi; the endpoint glue in AdminEndpoints stays a thin,
// tolerant wrapper whose only untestable line is the live GetMonitoringApi() call (E2E-verified).
// Honest-sparse: it reports only the jobs that actually run (the P8c-1 action-reminder sweep) — never
// an invented catalog. Absolute UTC timestamps go out; the SPA renders the relative "when" per locale.
public static class JobsMonitorMapper
{
    // Cap per state and on the merged list — the on-prem, ≤20-user stack runs a handful of jobs, not thousands.
    private const int RecentPerState = 20;

    public static JobsDto Map(IMonitoringApi api)
    {
        var stats = api.GetStatistics();
        var counts = new JobCountsDto(
            stats.Succeeded, stats.Processing, stats.Scheduled, stats.Enqueued, stats.Failed);

        var rows = new List<JobRowDto>();

        foreach (var (id, dto) in api.FailedJobs(0, RecentPerState))
            rows.Add(Row(id, dto.Job, JobStatuses.Failed, dto.FailedAt, durationMs: null, canRetry: true));

        foreach (var (id, dto) in api.ProcessingJobs(0, RecentPerState))
            rows.Add(Row(id, dto.Job, JobStatuses.Processing, dto.StartedAt, durationMs: null, canRetry: false));

        foreach (var (id, dto) in api.ScheduledJobs(0, RecentPerState))
            rows.Add(Row(id, dto.Job, JobStatuses.Scheduled, dto.EnqueueAt, durationMs: null, canRetry: false));

        foreach (var queue in api.Queues())
            foreach (var (id, dto) in queue.FirstJobs)
                rows.Add(Row(id, dto.Job, JobStatuses.Enqueued, dto.EnqueuedAt, durationMs: null, canRetry: false));

        foreach (var (id, dto) in api.SucceededJobs(0, RecentPerState))
            rows.Add(Row(id, dto.Job, JobStatuses.Succeeded, dto.SucceededAt, dto.TotalDuration, canRetry: false));

        var recent = rows
            .OrderByDescending(r => r.Timestamp ?? DateTimeOffset.MinValue)
            .Take(RecentPerState)
            .ToArray();

        return new JobsDto(Configured: true, counts, recent);
    }

    private static JobRowDto Row(string id, Job? job, string status, DateTime? whenUtc, long? durationMs, bool canRetry)
    {
        // job is null when Hangfire can't deserialize the invocation (assembly/method gone) — surface it honestly.
        var name = job?.Method?.Name ?? "(unknown)";
        var queue = string.IsNullOrWhiteSpace(job?.Queue) ? "default" : job!.Queue!;
        DateTimeOffset? ts = whenUtc is { } w
            ? new DateTimeOffset(DateTime.SpecifyKind(w, DateTimeKind.Utc))
            : null;
        return new JobRowDto(id, name, queue, status, ts, durationMs, canRetry);
    }
}

// Closed set of the five statuses the design renders. Anything Hangfire adds later (Deleted/Awaiting) is
// simply not surfaced here rather than leaking a raw, un-localized state name into the UI.
public static class JobStatuses
{
    public const string Succeeded = "Succeeded";
    public const string Processing = "Processing";
    public const string Scheduled = "Scheduled";
    public const string Enqueued = "Enqueued";
    public const string Failed = "Failed";
}

// Configured=false when Hangfire isn't wired (the "Testing" host / no connection string) — the SPA then
// renders "job monitoring not configured", the same honesty the System Health tab uses for unmonitored services.
public sealed record JobsDto(bool Configured, JobCountsDto Counts, IReadOnlyList<JobRowDto> Jobs)
{
    public static readonly JobsDto NotConfigured =
        new(false, new JobCountsDto(0, 0, 0, 0, 0), Array.Empty<JobRowDto>());
}

public sealed record JobCountsDto(long Succeeded, long Processing, long Scheduled, long Enqueued, long Failed);

public sealed record JobRowDto(
    string Id, string Name, string Queue, string Status, DateTimeOffset? Timestamp, long? DurationMs, bool CanRetry);
