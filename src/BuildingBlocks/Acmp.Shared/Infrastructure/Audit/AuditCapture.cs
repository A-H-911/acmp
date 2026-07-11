using System.Text.Json;
using Acmp.Shared.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Acmp.Shared.Infrastructure.Audit;

// ADR-0026 (PR1 step 3) — automatic before/after capture for the enriched AuditEvent.
//
// The interceptor observes every module SaveChanges and records, per mutated AuditableEntity, the changed
// SCALAR properties (before/after) — "changed-field deltas", not full graphs, per audit-and-records.md
// C-PRIV-01. Captures land in a request-scoped AuditChangeBuffer keyed by (SubjectType, PublicId); the
// enriched IAuditSink drains the matching capture when the handler emits its governance event (which owns the
// action verb). Denials/system events never mutate an entity, so they simply find no capture — correct.

// One captured mutation. SubjectId is the entity's stable PublicId (BaseEntity), which handlers also pass to
// the enriched sink, so the two correlate exactly.
public sealed record AuditChange(string SubjectType, string SubjectId, string? BeforeJson, string? AfterJson);

// Request-scoped list of captures, drained by the enriched sink. Not thread-safe: a DbContext/scope is used
// by one logical flow at a time.
public sealed class AuditChangeBuffer
{
    private readonly List<AuditChange> _changes = new();

    public void Add(AuditChange change) => _changes.Add(change);

    // Remove and return the first capture for this subject. With an id, matches (type + id) EXACTLY — never
    // guesses a different id. Without an id, matches by type (single-entity case). Returns null if none.
    public AuditChange? Take(string subjectType, string? subjectId)
    {
        var i = subjectId is null
            ? _changes.FindIndex(c => c.SubjectType == subjectType)
            : _changes.FindIndex(c => c.SubjectType == subjectType && c.SubjectId == subjectId);
        if (i < 0)
            return null;
        var change = _changes[i];
        _changes.RemoveAt(i);
        return change;
    }
}

public sealed class AuditCaptureInterceptor : SaveChangesInterceptor
{
    // Stamp/identity columns are not domain changes — keep them out of the delta.
    private static readonly HashSet<string> Ignored = new(StringComparer.Ordinal)
    {
        nameof(BaseEntity.PublicId), nameof(AuditableEntity.CreatedAt), nameof(AuditableEntity.CreatedBy),
        nameof(AuditableEntity.UpdatedAt), nameof(AuditableEntity.UpdatedBy),
    };

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly AuditChangeBuffer _buffer;

    public AuditCaptureInterceptor(AuditChangeBuffer buffer) => _buffer = buffer;

    // The app persists exclusively via async SaveChangesAsync, so only the async hook is overridden.
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        if (context is null) return;

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var before = new Dictionary<string, object?>();
            var after = new Dictionary<string, object?>();

            foreach (var p in entry.Properties)
            {
                var name = p.Metadata.Name;
                if (p.Metadata.IsPrimaryKey() || Ignored.Contains(name))
                    continue;

                switch (entry.State)
                {
                    case EntityState.Added:
                        after[name] = p.CurrentValue;
                        break;
                    case EntityState.Deleted:
                        before[name] = p.OriginalValue;
                        break;
                    case EntityState.Modified when p.IsModified:
                        before[name] = p.OriginalValue;
                        after[name] = p.CurrentValue;
                        break;
                }
            }

            _buffer.Add(new AuditChange(
                entry.Metadata.ClrType.Name,
                entry.Entity.PublicId.ToString(),
                before.Count == 0 ? null : JsonSerializer.Serialize(before, JsonOpts),
                after.Count == 0 ? null : JsonSerializer.Serialize(after, JsonOpts)));
        }
    }
}
