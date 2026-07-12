namespace Acmp.Shared.Application.Abstractions;

// Emits an audit/authorization signal. The implementation is SqlAuditSink (BL-066): the durable,
// immutable, hash-chained AuditEvent store (schema "audit") that also mirrors to Serilog -> Seq. This
// seam is exactly why swapping the P4 interim log-only sink for the durable store touched no call sites.
// ADR-0009. (Per-ballot crypto chaining over Vote/Ballot rows themselves is a separate P14 refinement;
// every vote/decision STATE CHANGE is already hash-chained here as an AuditEvent.)
public interface IAuditSink
{
    // Legacy (v1) — the lean row. Retained as the compatibility overload so the ~80 existing emit sites keep
    // compiling until they migrate to EmitEnrichedAsync (ADR-0026 PR2). Writes a HashVersion=1 row.
    Task EmitAsync(string eventType, string? subject, object? data = null, CancellationToken ct = default);

    // Enriched (v2) — the self-describing governance record (ADR-0026 / audit-and-records.md §1.1). The
    // handler supplies the action verb + subject; before/after are drained from the request-scoped capture
    // buffer by (subjectType, subjectId); actor/role come from the current principal; correlation from the
    // ambient OTel trace. A denial/system event simply finds no capture (null before/after) — correct.
    Task EmitEnrichedAsync(string action, string subjectType, string? subjectId,
        string outcome = AuditOutcome.Success, CancellationToken ct = default);
}

// The audit Outcome vocabulary (audit-and-records.md §1.1). Denied captures authZ denials; Failure captures
// a failed operation.
public static class AuditOutcome
{
    public const string Success = "Success";
    public const string Denied = "Denied";
    public const string Failure = "Failure";
}
