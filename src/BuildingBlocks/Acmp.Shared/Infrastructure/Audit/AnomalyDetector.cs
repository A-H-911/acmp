using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Shared.Infrastructure.Audit;

// C-INS-01 / NFR-065 — detection for the two insider-threat anomaly signals. It lives HERE, beside
// AuditChainIntegrityCheck, because both are cross-cutting readers of the audit store rather than any
// module's business: a module may not read another module's tables (ADR-0001), and the audit log is nobody's
// module.
//
// ⚠⚠ EVERY THRESHOLD IS PROVISIONAL AND CONFIGURABLE — DEC-099 d3, and DEF-110 is why. Both DW-087 and
// DW-092 say a threshold chosen today is a guess dressed as a control: neither endpoint has usage history,
// so no value here is derived from anything. NFR-065's second clause exists precisely so that "provisional
// pending a baseline" is a LEGITIMATE recorded answer. The values live in the ConfigurationSetting store
// WBS-24.5 built rather than in a hardcoded switch, because DEF-110 is this project's open record of the
// opposite: urgency SLA thresholds hardcoded 3/7/21 while ASM-011 and OQ-035 both promise the committee will
// adjust them via configuration — so the remediation path does not exist.
public sealed class AnomalyDetector : IAnomalyDetector
{
    // ⚠ THE DEFAULTS ARE DELIBERATELY LOOSE, NOT TUNED. An untuned detector that cries wolf teaches its
    // reader to ignore it, which is worse than one that stays quiet until a baseline exists. They are a
    // starting point to be replaced from observed usage, and the config keys are the seam for doing it
    // without a deployment.
    // ⚠⚠ LOWER-CASE, AND THAT IS NOT A STYLE CHOICE. ConfigurationSetting.Create NORMALISES every key
    // with Trim().ToLowerInvariant() before storing it, so a reader matching a camelCase string finds
    // NOTHING and silently falls back to its default - a threshold that cannot be configured, which is
    // DEF-110's shape arriving through the back door on the very control built to avoid it. The first
    // version of this class used camelCase keys and both anomaly tests failed with the defaults in force.
    public const string BulkExportRowsKey = "anomaly.bulkexport.rowcount";
    public const string RestrictedAccessCountKey = "anomaly.restrictedaccess.count";
    public const string RestrictedAccessWindowKey = "anomaly.restrictedaccess.windowminutes";

    private const int DefaultBulkExportRows = 500;
    private const int DefaultRestrictedAccessCount = 20;
    private const int DefaultRestrictedAccessWindowMinutes = 60;

    public const string AccessEvent = "Topics.RestrictedTopicAccessed";
    public const string BulkExportAnomalyEvent = "Audit.BulkExportAnomaly";
    public const string RestrictedAccessAnomalyEvent = "Topics.RestrictedAccessAnomaly";

    private readonly IAuditSink _audit;
    private readonly AuditDbContext _auditDb;
    private readonly ConfigurationDbContext _config;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public AnomalyDetector(IAuditSink audit, AuditDbContext auditDb, ConfigurationDbContext config,
        ICurrentUser user, IClock clock)
    {
        _audit = audit;
        _auditDb = auditDb;
        _config = config;
        _user = user;
        _clock = clock;
    }

    public async Task ObserveAuditExportAsync(int rowCount, CancellationToken ct = default)
    {
        var threshold = await ReadIntAsync(BulkExportRowsKey, DefaultBulkExportRows, ct);
        if (rowCount < threshold) return;

        // ⚠ >= NOT >, AND THE BOUNDARY IS PART OF THE CONTRACT. A threshold named "the volume at which this
        // is atypical" that does not fire AT that volume is off by one in the direction that misses events.
        await _audit.EmitEnrichedAsync(BulkExportAnomalyEvent, "AuditExport", rowCount.ToString(), ct: ct);
    }

    public async Task ObserveRestrictedTopicAccessAsync(Guid topicId, CancellationToken ct = default)
    {
        // RECORD FIRST, ALWAYS. This is the half that did not exist (DEC-099 d1) and it is valuable on its
        // own: without it nothing can answer "who read this Restricted topic", whatever any detection rule
        // later decides. The topic id only — never its content — following Meetings.RecordingAccessed.
        await _audit.EmitEnrichedAsync(AccessEvent, "Topic", topicId.ToString(), ct: ct);

        var actor = _user.UserId;
        if (string.IsNullOrWhiteSpace(actor)) return;

        var threshold = await ReadIntAsync(RestrictedAccessCountKey, DefaultRestrictedAccessCount, ct);
        var windowMinutes = await ReadIntAsync(RestrictedAccessWindowKey, DefaultRestrictedAccessWindowMinutes, ct);
        var since = _clock.UtcNow.AddMinutes(-windowMinutes);

        // ⚠ `Action ?? EventType`, THE SAME COALESCE RefusalAuditTests DOCUMENTS AT LENGTH. The store holds
        // two row shapes — EmitAsync writes a lean row with EventType set and Action NULL, EmitEnrichedAsync
        // writes both — so keying on Action alone silently counts nothing for half the log. This detector
        // emits enriched rows, but the query must not depend on that staying true.
        var count = await _auditDb.AuditEvents.AsNoTracking()
            .Where(e => e.ActorUserId == actor
                        && e.OccurredAt >= since
                        && (e.Action ?? e.EventType) == AccessEvent)
            .CountAsync(ct);

        if (count < threshold) return;

        await _audit.EmitEnrichedAsync(RestrictedAccessAnomalyEvent, "Topic", topicId.ToString(), ct: ct);
    }

    // A missing or unparseable setting falls back to the default rather than throwing. ⚠ THAT IS THE RIGHT
    // BIAS HERE AND WOULD BE THE WRONG ONE IN AN AUTHORIZATION PATH: detection runs on the success path
    // (IAnomalyDetector's contract says so), and a monitoring control that turned a bad config value into a
    // failed read would convert an observability gap into an outage.
    private async Task<int> ReadIntAsync(string key, int fallback, CancellationToken ct)
    {
        var raw = await _config.Settings.AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.ValueJson)
            .FirstOrDefaultAsync(ct);

        return int.TryParse(raw, out var value) && value > 0 ? value : fallback;
    }
}
