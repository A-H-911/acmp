namespace Acmp.Shared.Application.Abstractions;

// Emits an audit/authorization signal. P4 interim implementation writes to the structured log
// (Serilog -> Seq) so authorization denials and auth events are recorded now (AC-003/006/010).
// The immutable, hash-chained AuditEvent store is BL-066 (P9 sequencing, before votes); this
// interface is the seam those handlers will re-bind to without touching call sites. ADR-0009.
public interface IAuditSink
{
    Task EmitAsync(string eventType, string? subject, object? data = null, CancellationToken ct = default);
}
