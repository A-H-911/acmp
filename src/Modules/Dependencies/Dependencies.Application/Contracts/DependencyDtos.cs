namespace Acmp.Modules.Dependencies.Application.Contracts;

// Read models returned to the SPA. Enums (endpoint type, kind, status) project as their string names — a
// stable wire contract the SPA localizes. Both endpoints carry their create-time deep-link key + title
// snapshots (ADR-0019); dependencies never join another module's tables (ADR-0001). IsBlocker is DERIVED
// (Kind ∈ {BlockedBy, Blocks} && Status == Open), computed at read time, never stored.

// Full detail for one edge (by key).
public sealed record DependencyDto(
    Guid Id,
    string Key,
    string FromType,
    Guid FromId,
    string FromKey,
    string FromTitle,
    string ToType,
    Guid ToId,
    string ToKey,
    string ToTitle,
    string Kind,
    string Status,
    string? Note,
    bool IsBlocker,
    DateTimeOffset CreatedAt);

// One row of the register (the dependencies table). Carries both endpoints + kind/status + the derived
// blocker flag.
public sealed record DependencySummaryDto(
    Guid Id,
    string Key,
    string FromType,
    Guid FromId,
    string FromKey,
    string FromTitle,
    string ToType,
    Guid ToId,
    string ToKey,
    string ToTitle,
    string Kind,
    string Status,
    bool IsBlocker);

// One edge on an artifact's dependency panel — the FAR endpoint (relative to the viewed artifact) plus the
// kind/status and the derived blocker flag. Direction is implied by which list it appears in (Outbound =
// this artifact is the From end; Inbound = this artifact is the To end).
public sealed record DependencyEdgeDto(
    Guid Id,
    string Key,
    string OtherType,
    Guid OtherId,
    string OtherKey,
    string OtherTitle,
    string Kind,
    string Status,
    bool IsBlocker);

// The artifact's dependency panel: its outbound and inbound edges (Removed edges excluded).
public sealed record ArtifactDependenciesDto(
    IReadOnlyList<DependencyEdgeDto> Outbound,
    IReadOnlyList<DependencyEdgeDto> Inbound);
