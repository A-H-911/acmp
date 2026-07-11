using System.Diagnostics;
using System.Text.Json;
using Acmp.Shared.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Acmp.Shared.Infrastructure.Audit;

// BL-066 (ADR-0009, enriched by ADR-0026) — the durable IAuditSink: appends each state change to the
// immutable, hash-chained AuditEvent store (schema "audit") and ALSO mirrors it to the structured log
// (Serilog -> Seq, ADR-0014) so the existing observability stream is unbroken.
//
// Fail-closed: if the append cannot be persisted the exception propagates (the operation is not silently
// recorded as unaudited) — correct for a governance system of record.
//
// ponytail: no application-level lock. Chain linearity is enforced by the UNIQUE index on PreviousHash; a
// genuinely concurrent append loses the race with a unique-constraint violation. At this deployment's scale
// (on-prem, <=20 users, low traffic) that window is negligible. If throughput ever makes it bite, serialize
// appends (sp_getapplock) or partition the chain per stream — not before.
public sealed class SqlAuditSink : IAuditSink
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly AuditDbContext _db;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly AuditChangeBuffer _buffer;
    private readonly ILogger<SqlAuditSink> _logger;

    public SqlAuditSink(AuditDbContext db, IClock clock, ICurrentUser currentUser, AuditChangeBuffer buffer,
        ILogger<SqlAuditSink> logger)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
        _buffer = buffer;
        _logger = logger;
    }

    // Legacy (v1) — the lean row. Unchanged behaviour; still used by not-yet-migrated call sites.
    public async Task EmitAsync(string eventType, string? subject, object? data = null, CancellationToken ct = default)
    {
        var dataJson = data is null ? null : JsonSerializer.Serialize(data, JsonOpts);
        var evt = AuditEvent.CreateNext(await TipHashAsync(ct), _clock.UtcNow, eventType, subject, dataJson);
        await AppendAsync(evt, ct);
        _logger.LogInformation(
            "AuditEvent {AuditEventType} by {AuditSubject} seq={AuditSequence} (Audit=true)",
            eventType, subject ?? "anonymous", evt.Sequence);
    }

    // Enriched (v2) — the self-describing governance record (ADR-0026 / audit-and-records.md §1.1).
    public async Task EmitEnrichedAsync(string action, string subjectType, string? subjectId,
        string outcome = AuditOutcome.Success, CancellationToken ct = default)
    {
        // Before/after captured by the SaveChanges interceptor for this exact subject (or none, for a
        // denial/system event that changed no entity). Fall back to the captured id if the caller omitted it.
        var change = _buffer.Take(subjectType, subjectId);
        var actor = _currentUser.UserId;
        var actorRole = _currentUser.Roles.Count > 0 ? string.Join(",", _currentUser.Roles) : null;
        var correlationId = Activity.Current?.TraceId.ToString();

        var evt = AuditEvent.CreateEnriched(await TipHashAsync(ct), _clock.UtcNow, action, subjectType,
            subjectId ?? change?.SubjectId, actor, actorRole, outcome, change?.BeforeJson, change?.AfterJson,
            correlationId);
        await AppendAsync(evt, ct);
        _logger.LogInformation(
            "AuditEvent {AuditAction} {AuditOutcome} on {AuditSubjectType}/{AuditSubjectId} by {AuditActor} seq={AuditSequence} (Audit=true)",
            action, outcome, subjectType, subjectId ?? "-", actor ?? "system", evt.Sequence);
    }

    private async Task<string> TipHashAsync(CancellationToken ct) =>
        await _db.AuditEvents.OrderByDescending(e => e.Sequence).Select(e => e.Hash).FirstOrDefaultAsync(ct)
        ?? AuditEvent.Genesis;

    private async Task AppendAsync(AuditEvent evt, CancellationToken ct)
    {
        _db.AuditEvents.Add(evt);
        await _db.SaveChangesAsync(ct);
    }
}
