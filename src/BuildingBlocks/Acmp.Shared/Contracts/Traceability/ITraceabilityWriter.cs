namespace Acmp.Shared.Contracts.Traceability;

// Cross-module WRITE seam (ADR-0001, P11e): lets another module record a system-initiated typed edge in the
// Traceability store without referencing Traceability.Application or its enums. Implemented in
// Traceability.Infrastructure over the same Relationship store the UI CreateRelationship uses (one source of
// truth). Unlike the user-driven CreateRelationship command this carries no RBAC of its own — the calling
// action is already authorized (e.g. FR-068 promotion is Chairman-gated) and the edge is a consequence of it.
// The RelType is passed as its enum NAME (e.g. "RecordedAs") so the Traceability vocabulary never leaks into
// the shared kernel; an unknown name is rejected by the impl.
public interface ITraceabilityWriter
{
    // Create a directed Source → Target edge with key + title snapshots (so the panel renders without reading
    // the owning modules' tables). Idempotent per (source, target, relType): a duplicate is a no-op.
    Task RecordEdgeAsync(
        string sourceType, Guid sourceId, string sourceKey, string sourceTitle,
        string targetType, Guid targetId, string targetKey, string targetTitle,
        string relTypeName, CancellationToken ct = default);
}
