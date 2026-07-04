using Acmp.Modules.Traceability.Application.Abstractions;
using Acmp.Modules.Traceability.Domain;
using Acmp.Modules.Traceability.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Traceability;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Traceability.Infrastructure.Directory;

// ITraceabilityWriter impl (P11e) — writes a system-initiated typed edge over the same Relationship store the
// UI CreateRelationship uses. No RBAC of its own (the calling action is already authorized). Idempotent per
// (source, target, relType) so a retried promotion never duplicates the edge. Audited like a user edge, with a
// System=true marker so the trail distinguishes the automatic write.
public sealed class TraceabilityWriter : ITraceabilityWriter
{
    private readonly ITraceabilityDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditSink _audit;

    public TraceabilityWriter(ITraceabilityDbContext db, ICurrentUser user, IAuditSink audit)
    {
        _db = db;
        _user = user;
        _audit = audit;
    }

    public async Task RecordEdgeAsync(
        string sourceType, Guid sourceId, string sourceKey, string sourceTitle,
        string targetType, Guid targetId, string targetKey, string targetTitle,
        string relTypeName, CancellationToken ct = default)
    {
        if (!Enum.TryParse<ArtifactType>(sourceType, out var st)
            || !Enum.TryParse<ArtifactType>(targetType, out var tt)
            || !Enum.TryParse<RelationshipType>(relTypeName, out var rt))
            throw new InvalidOperationException("Unknown artifact or relationship type for a traceability edge.");

        var exists = await _db.Relationships.AnyAsync(r =>
            r.IsActive && r.RelType == rt
            && r.SourceType == st && r.SourceId == sourceId
            && r.TargetType == tt && r.TargetId == targetId, ct);
        if (exists) return;

        var edge = Relationship.Create(st, sourceId, sourceKey, sourceTitle, tt, targetId, targetKey, targetTitle, rt, notes: null);
        _db.Relationships.Add(edge);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitAsync("Relationship.Created", _user.UserId, new
        {
            edge.PublicId,
            SourceType = st.ToString(),
            edge.SourceId,
            edge.SourceKey,
            TargetType = tt.ToString(),
            edge.TargetId,
            edge.TargetKey,
            RelType = rt.ToString(),
            System = true,
        }, ct);
    }
}
