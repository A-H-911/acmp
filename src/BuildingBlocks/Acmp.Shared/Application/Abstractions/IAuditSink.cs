namespace Acmp.Shared.Application.Abstractions;

// Emits an audit/authorization signal. The implementation is SqlAuditSink (BL-066): the durable,
// immutable, hash-chained AuditEvent store (schema "audit") that also mirrors to Serilog -> Seq. This
// seam is exactly why swapping the P4 interim log-only sink for the durable store touched no call sites.
// ADR-0009. (Per-ballot crypto chaining over Vote/Ballot rows themselves is a separate P14 refinement;
// every vote/decision STATE CHANGE is already hash-chained here as an AuditEvent.)
public interface IAuditSink
{
    Task EmitAsync(string eventType, string? subject, object? data = null, CancellationToken ct = default);
}
