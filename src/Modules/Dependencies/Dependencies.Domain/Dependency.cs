using Acmp.Modules.Dependencies.Domain.Enums;
using Acmp.Shared.Domain.Entities;

namespace Acmp.Modules.Dependencies.Domain;

// The Dependency aggregate root — a typed directed edge between two governed artifacts:
// (FromType, FromId) --Kind--> (ToType, ToId). Both endpoints are SELF-DESCRIBING value snapshots (key +
// title captured at create time, ADR-0019) — never an EF navigation into the owning module (ADR-0001). The
// edge is retracted by moving Status to Removed (a soft state, not a hard delete) so the historical link is
// preserved for audit. CreatedBy/At come from AuditableEntity (stamped from the current user).
public sealed class Dependency : AuditableEntity
{
    private Dependency() { }

    // Optimistic-concurrency token (SQL rowversion). A stale write throws DbUpdateConcurrencyException → 409.
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public string Key { get; private set; } = string.Empty;          // DPN-YYYY-###

    public DependencyEndpointType FromType { get; private set; }
    public Guid FromId { get; private set; }
    public string FromKey { get; private set; } = string.Empty;      // e.g. TOP-2026-042 (immutable, deep-linkable)
    public string FromTitle { get; private set; } = string.Empty;    // create-time snapshot (may go stale, ADR-0019)

    public DependencyEndpointType ToType { get; private set; }
    public Guid ToId { get; private set; }
    public string ToKey { get; private set; } = string.Empty;
    public string ToTitle { get; private set; } = string.Empty;

    public DependencyKind Kind { get; private set; }
    public DependencyStatus Status { get; private set; }
    public string? Note { get; private set; }                        // optional human annotation

    // Create a typed directed edge. Endpoints must be distinct artifacts (no self-loop) and carry their
    // display-key + title snapshots (required — the panel renders them). Command-level validation runs first
    // (FluentValidation); these guards are the domain's defence in depth. Starts Open.
    public static Dependency Create(
        string key,
        DependencyEndpointType fromType, Guid fromId, string fromKey, string fromTitle,
        DependencyEndpointType toType, Guid toId, string toKey, string toTitle,
        DependencyKind kind, string? note)
    {
        if (fromId == Guid.Empty || toId == Guid.Empty)
            throw new InvalidOperationException("Both endpoints of a dependency are required.");
        if (fromType == toType && fromId == toId)
            throw new InvalidOperationException("A dependency cannot link an artifact to itself.");
        if (!Enum.IsDefined(kind)) throw new InvalidOperationException("A valid dependency kind is required.");

        return new Dependency
        {
            Key = (key ?? string.Empty).Trim(),
            FromType = fromType,
            FromId = fromId,
            FromKey = (fromKey ?? string.Empty).Trim(),
            FromTitle = (fromTitle ?? string.Empty).Trim(),
            ToType = toType,
            ToId = toId,
            ToKey = (toKey ?? string.Empty).Trim(),
            ToTitle = (toTitle ?? string.Empty).Trim(),
            Kind = kind,
            Status = DependencyStatus.Open,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        };
    }

    // Mark the dependency satisfied. Only a live (Open) edge can be resolved.
    public void Resolve()
    {
        if (Status != DependencyStatus.Open)
            throw new InvalidOperationException("Only an open dependency can be resolved.");
        Status = DependencyStatus.Resolved;
    }

    // Retract an edge created in error (soft-delete): the row stays for the audit trail. Only a live (Open)
    // edge can be removed.
    public void Remove()
    {
        if (Status != DependencyStatus.Open)
            throw new InvalidOperationException("Only an open dependency can be removed.");
        Status = DependencyStatus.Removed;
    }
}
