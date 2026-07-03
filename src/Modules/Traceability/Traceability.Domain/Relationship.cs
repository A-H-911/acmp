using Acmp.Modules.Traceability.Domain.Enums;
using Acmp.Shared.Domain.Entities;

namespace Acmp.Modules.Traceability.Domain;

// The Relationship aggregate root (ADR-0008, docs/30 §2) — a typed directed edge in the traceability graph:
// (SourceType, SourceId) --RelType--> (TargetType, TargetId). Both endpoints are SELF-DESCRIBING value
// snapshots (key + title captured at create time, ADR-0019) — never an EF navigation into the owning module
// (ADR-0001). No physical Artifact registry. Edges are SOFT-deleted (IsActive=0), never hard-deleted, so the
// historical link is preserved for audit (docs/30 §5, ADR-0009). CreatedBy/At come from AuditableEntity
// (stamped from the current user); deactivation records who/when explicitly.
public sealed class Relationship : AuditableEntity
{
    private Relationship() { }

    public ArtifactType SourceType { get; private set; }
    public Guid SourceId { get; private set; }
    public string SourceKey { get; private set; } = string.Empty;    // e.g. TOP-2026-042 (immutable, deep-linkable)
    public string SourceTitle { get; private set; } = string.Empty;  // create-time snapshot (may go stale, ADR-0019)

    public ArtifactType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public string TargetKey { get; private set; } = string.Empty;
    public string TargetTitle { get; private set; } = string.Empty;

    public RelationshipType RelType { get; private set; }
    public string? Notes { get; private set; }                       // optional human annotation (docs/30 §2.1)

    public bool IsActive { get; private set; }
    public DateTimeOffset? DeactivatedAt { get; private set; }
    public string? DeactivatedByUserId { get; private set; }

    // Create a typed directed edge. Endpoints must be distinct artifacts (no self-loop) and carry their
    // display-key + title snapshots (required — the panel renders them, AC-062). Command-level validation runs
    // first (FluentValidation); these guards are the domain's defence in depth.
    public static Relationship Create(
        ArtifactType sourceType, Guid sourceId, string sourceKey, string sourceTitle,
        ArtifactType targetType, Guid targetId, string targetKey, string targetTitle,
        RelationshipType relType, string? notes)
    {
        if (sourceId == Guid.Empty || targetId == Guid.Empty)
            throw new InvalidOperationException("Both endpoints of a relationship are required.");
        if (sourceType == targetType && sourceId == targetId)
            throw new InvalidOperationException("A relationship cannot link an artifact to itself.");
        if (!Enum.IsDefined(relType)) throw new InvalidOperationException("A valid relationship type is required.");

        return new Relationship
        {
            SourceType = sourceType,
            SourceId = sourceId,
            SourceKey = (sourceKey ?? string.Empty).Trim(),
            SourceTitle = (sourceTitle ?? string.Empty).Trim(),
            TargetType = targetType,
            TargetId = targetId,
            TargetKey = (targetKey ?? string.Empty).Trim(),
            TargetTitle = (targetTitle ?? string.Empty).Trim(),
            RelType = relType,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            IsActive = true,
        };
    }

    // Soft-delete (docs/30 §5): the edge stays in the table with IsActive=0 for the audit trail. Idempotent —
    // deactivating an already-inactive edge is a no-op (the handler still audits the attempt).
    public void Deactivate(string byUserId, DateTimeOffset now)
    {
        if (!IsActive) return;
        IsActive = false;
        DeactivatedAt = now;
        DeactivatedByUserId = byUserId;
    }
}
