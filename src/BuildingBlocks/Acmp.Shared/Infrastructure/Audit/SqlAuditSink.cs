using System.Diagnostics;
using System.Text.Json;
using Acmp.Shared.Application.Abstractions;
using Microsoft.Data.SqlClient;
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
// Chain linearity is enforced by the UNIQUE indexes on PreviousHash and Hash: two concurrent appends off the
// same tip fork the chain, and the loser hits a duplicate-key (SQL 2601/2627). D-18 / ADR-0028 removes that at
// the source for WRITE-commands by serializing them on a transaction-scoped app lock taken at tx-open
// (AmbientTransaction) — so a normal command's audit append never races. This retry remains the backstop for the
// DENIAL / autocommit path: a denial writes no module entity, so it opens no ambient transaction and holds no
// tx-open lock; concurrent denials can still fork (and, being autocommit with no crosswise module locks, only
// fork — never deadlock). AppendAsync catches the tip-race and RETRIES — re-reads the now-advanced tip,
// recomputes, re-inserts — bounded, so it still fails closed if it genuinely cannot persist.
public sealed class SqlAuditSink : IAuditSink
{
    // Upper bound on tip-race retries. One append can only lose to the *other* concurrent appenders in flight, so
    // this comfortably exceeds any realistic simultaneous-writer count at <=20 users; exhausting it throws (fail-
    // closed) rather than looping forever.
    private const int MaxAppendAttempts = 16;

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
        var now = _clock.UtcNow;
        var evt = await AppendAsync(prev => AuditEvent.CreateNext(prev, now, eventType, subject, dataJson), ct);
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
        var now = _clock.UtcNow;
        // The before/after (Take, consumed once) and every field but PreviousHash are fixed for this event; only
        // the tip varies across retries, so the closure rebuilds off the freshly-read tip on each attempt.
        var evt = await AppendAsync(prev => AuditEvent.CreateEnriched(prev, now, action, subjectType,
            subjectId ?? change?.SubjectId, actor, actorRole, outcome, change?.BeforeJson, change?.AfterJson,
            correlationId), ct);
        _logger.LogInformation(
            "AuditEvent {AuditAction} {AuditOutcome} on {AuditSubjectType}/{AuditSubjectId} by {AuditActor} seq={AuditSequence} (Audit=true)",
            action, outcome, subjectType, subjectId ?? "-", actor ?? "system", evt.Sequence);
    }

    private async Task<string> TipHashAsync(CancellationToken ct) =>
        await _db.AuditEvents.OrderByDescending(e => e.Sequence).Select(e => e.Hash).FirstOrDefaultAsync(ct)
        ?? AuditEvent.Genesis;

    // Build the row off the current tip and insert it; on a PreviousHash tip-race (a concurrent appender took the
    // same tip first) drop the failed row, re-read the advanced tip and rebuild, up to MaxAppendAttempts. The
    // catch is narrow — ONLY the PreviousHash duplicate-key — so a Hash-index violation or any other error still
    // surfaces (fail-closed). Each SaveChanges either commits within the ambient transaction or, on the last
    // attempt, rethrows so the whole command rolls back rather than proceeding unaudited.
    private async Task<AuditEvent> AppendAsync(Func<string, AuditEvent> build, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var evt = build(await TipHashAsync(ct));
            _db.AuditEvents.Add(evt);
            try
            {
                await _db.SaveChangesAsync(ct);
                return evt;
            }
            catch (DbUpdateException ex) when (attempt < MaxAppendAttempts && IsTipRace(ex))
            {
                _db.Entry(evt).State = EntityState.Detached;
                _logger.LogDebug("Audit tip-race on attempt {AuditAppendAttempt}; re-reading tip and retrying", attempt);
            }
        }
    }

    // A concurrent-append collision on a chain-linearity index — retryable. Narrowed to the two audit-chain
    // UNIQUE indexes by name so an unrelated unique violation is NOT swallowed. Both are tip-race artifacts:
    // PreviousHash collides when two appends share a tip; Hash collides when two IDENTICAL appends share a tip
    // (same actor/subject/clock) — re-reading the advanced tip changes PreviousHash and so the Hash too.
    private static bool IsTipRace(DbUpdateException ex) =>
        ex.InnerException is SqlException sql
        && sql.Number is 2601 or 2627
        && (sql.Message.Contains("IX_AuditEvents_PreviousHash", StringComparison.Ordinal)
            || sql.Message.Contains("IX_AuditEvents_Hash", StringComparison.Ordinal));
}
