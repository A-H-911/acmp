namespace Acmp.Shared.Application.Abstractions;

// C-INS-01 (NFR-065): the two insider-threat anomaly signals - atypical bulk-export volume, and atypical
// access to Restricted topics. The caller reports that something HAPPENED; this decides whether it is
// atypical and emits a distinct audit event if so.
//
// ⚠ THE APPLICATION DETECTS AND SEQ NOTIFIES - DEC-099 d2, and the split is deliberate. C-INS-01 says
// "anomaly alerting via Seq", and C-AUDIT-05 says privileged actions "emit high-importance audit events
// FEEDING C-INS-01". So the threshold is evaluated HERE, in versioned and testable code, and the event this
// emits is what a Seq rule notifies on. The deciding argument was that a Seq alert rule is configuration no
// gate can see - the shape DEF-078 (a healthcheck evaluating zero checks), DEF-079 (an allowlist exempting
// every markdown file) and DW-091 (a healthcheck nobody consumes) all record.
//
// ⚠⚠ THE OBSERVE METHODS ARE ON THE SUCCESS PATH AND MUST NEVER REFUSE ANYTHING. These are detection, not
// authorization: the caller has already been permitted. A detector that threw would convert a monitoring
// control into an outage, which is strictly worse than the gap it closes.
public interface IAnomalyDetector
{
    /// <summary>An audit-log export completed. Emits a bulk-export anomaly when the delivered row count
    /// exceeds the configured threshold.</summary>
    /// <remarks>
    /// The volume is passed in rather than re-derived: the export endpoint already knows the delivered row
    /// count and already audits it (C-AUDIT-08), so counting it twice would be two instruments that can
    /// disagree.
    /// </remarks>
    Task ObserveAuditExportAsync(int rowCount, CancellationToken ct = default);

    /// <summary>A principal successfully read a <c>Restricted</c> topic. RECORDS the access, then emits an
    /// anomaly when that principal's accesses within the configured window exceed the threshold.</summary>
    /// <remarks>
    /// ⚠⚠ IT RECORDS AS WELL AS DETECTS, AND THAT IS THE HALF THAT WAS MISSING. Measured 2026-08-30: 18 write
    /// features in Topics emit audit events and ZERO read features do, and the whole ABAC path contains no
    /// audit sink - so nothing anywhere knew who had read which Restricted topic, and this signal was
    /// undetectable by any mechanism, Seq included. DW-092 stated the data was already accumulating; it was
    /// not (DEC-099 d1).
    ///
    /// The shape follows <c>Meetings.RecordingAccessed</c>, which is this codebase's existing precedent for
    /// auditing successful access to a sensitive resource: the subject id is recorded and the CONTENT never
    /// is, because an audit row is a log and the id is enough to answer "who reached this, and when".
    /// </remarks>
    Task ObserveRestrictedTopicAccessAsync(Guid topicId, CancellationToken ct = default);
}
