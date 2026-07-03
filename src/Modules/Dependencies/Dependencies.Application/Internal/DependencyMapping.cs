using Acmp.Modules.Dependencies.Application.Contracts;
using Acmp.Modules.Dependencies.Domain;
using Acmp.Modules.Dependencies.Domain.Enums;

namespace Acmp.Modules.Dependencies.Application.Internal;

// Aggregate → read-model projection, shared by the queries and the command return values. IsBlocker is the
// derived overlay (Kind ∈ {BlockedBy, Blocks} && Status == Open) — computed here, never a stored column.
internal static class DependencyMapping
{
    public static bool IsBlocker(Dependency d) =>
        (d.Kind == DependencyKind.BlockedBy || d.Kind == DependencyKind.Blocks) && d.Status == DependencyStatus.Open;

    public static DependencyDto ToDetail(Dependency d) => new(
        d.PublicId, d.Key,
        d.FromType.ToString(), d.FromId, d.FromKey, d.FromTitle,
        d.ToType.ToString(), d.ToId, d.ToKey, d.ToTitle,
        d.Kind.ToString(), d.Status.ToString(), d.Note, IsBlocker(d), d.CreatedAt);

    public static DependencySummaryDto ToSummary(Dependency d) => new(
        d.PublicId, d.Key,
        d.FromType.ToString(), d.FromId, d.FromKey, d.FromTitle,
        d.ToType.ToString(), d.ToId, d.ToKey, d.ToTitle,
        d.Kind.ToString(), d.Status.ToString(), IsBlocker(d));

    // Project from the perspective of the artifact whose panel is viewed: Outbound → the "other" endpoint is
    // the To end; Inbound → the "other" endpoint is the From end.
    public static DependencyEdgeDto ToOutboundEdge(Dependency d) => new(
        d.PublicId, d.Key, d.ToType.ToString(), d.ToId, d.ToKey, d.ToTitle,
        d.Kind.ToString(), d.Status.ToString(), IsBlocker(d));

    public static DependencyEdgeDto ToInboundEdge(Dependency d) => new(
        d.PublicId, d.Key, d.FromType.ToString(), d.FromId, d.FromKey, d.FromTitle,
        d.Kind.ToString(), d.Status.ToString(), IsBlocker(d));
}
