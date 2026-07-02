using System.Text.Json;
using Acmp.Shared.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Acmp.Shared.Infrastructure.Audit;

// BL-066 (ADR-0009) — the durable IAuditSink: appends each state change to the immutable, hash-chained
// AuditEvent store (schema "audit") and ALSO mirrors it to the structured log (Serilog -> Seq, ADR-0014)
// so the existing observability stream is unbroken. Call sites are unchanged — they always used the
// IAuditSink seam, which is exactly why swapping the interim SerilogAuditSink for this needs no edits
// beyond DI registration.
//
// Fail-closed: if the append cannot be persisted the exception propagates (the operation is not silently
// recorded as unaudited) — correct for a governance system of record.
//
// ponytail: no application-level lock. Chain linearity is enforced by the UNIQUE index on PreviousHash; a
// genuinely concurrent append loses the race with a unique-constraint violation (surfaced as a 500). At
// this deployment's scale (on-prem, <=20 users, low traffic) that window is negligible. If throughput ever
// makes it bite, serialize appends (sp_getapplock) or partition the chain per stream — not before.
public sealed class SqlAuditSink : IAuditSink
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly AuditDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<SqlAuditSink> _logger;

    public SqlAuditSink(AuditDbContext db, IClock clock, ILogger<SqlAuditSink> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task EmitAsync(string eventType, string? subject, object? data = null, CancellationToken ct = default)
    {
        var dataJson = data is null ? null : JsonSerializer.Serialize(data, JsonOpts);

        var tipHash = await _db.AuditEvents
            .OrderByDescending(e => e.Sequence)
            .Select(e => e.Hash)
            .FirstOrDefaultAsync(ct) ?? AuditEvent.Genesis;

        var evt = AuditEvent.CreateNext(tipHash, _clock.UtcNow, eventType, subject, dataJson);
        _db.AuditEvents.Add(evt);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "AuditEvent {AuditEventType} by {AuditSubject} seq={AuditSequence} (Audit=true)",
            eventType, subject ?? "anonymous", evt.Sequence);
    }
}
