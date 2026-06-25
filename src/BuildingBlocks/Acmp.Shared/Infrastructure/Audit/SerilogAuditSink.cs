using Acmp.Shared.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Acmp.Shared.Infrastructure.Audit;

// P4 interim IAuditSink: writes a structured, queryable audit record to the log pipeline
// (Serilog -> self-hosted Seq, ADR-0014). Tagged Audit=true so Seq can isolate the audit stream.
// ponytail: not the durable store — BL-066 replaces this with the append-only hash-chained
// AuditEvent table; call sites stay unchanged.
public sealed class SerilogAuditSink : IAuditSink
{
    private readonly ILogger<SerilogAuditSink> _logger;

    public SerilogAuditSink(ILogger<SerilogAuditSink> logger) => _logger = logger;

    public Task EmitAsync(string eventType, string? subject, object? data = null, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "AuditEvent {AuditEventType} by {AuditSubject} {@AuditData} (Audit=true)",
            eventType, subject ?? "anonymous", data);
        return Task.CompletedTask;
    }
}
