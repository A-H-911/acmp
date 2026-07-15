using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Research.Application.Contracts;

// Read models returned to the SPA. Enums project as their string names (stable wire contract, localized in the
// UI). Bilingual text is the LocalizedString value object (the SPA picks the locale). Missions never join
// another module's tables (ADR-0001); SourceTopicId / LinkedTopicId are opaque value ids (no key/title
// snapshot — the traceability graph edge is P15c). The register row is lean; the full mission is the detail.

public sealed record FindingDto(
    Guid Id,
    string Key,
    LocalizedString Summary,
    LocalizedString? Detail,
    string Confidence,
    bool IsVerified);

public sealed record RecommendationDto(
    Guid Id,
    string Key,
    LocalizedString Statement,
    LocalizedString? Rationale,
    string Priority,
    string Status,
    Guid? LinkedTopicId);

public sealed record ResearchMissionSummaryDto(
    Guid Id,
    string Key,
    LocalizedString Title,
    string Status,
    string OwnerName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    int FindingCount,
    int RecommendationCount);

public sealed record ResearchMissionDetailDto(
    Guid Id,
    string Key,
    LocalizedString Title,
    LocalizedString Question,
    string Status,
    string OwnerUserId,
    string OwnerName,
    string? KeystonePackageRef,
    Guid? SourceTopicId,
    DateTimeOffset? CompletedAt,
    LocalizedString? CancellationReason,
    IReadOnlyList<FindingDto> Findings,
    IReadOnlyList<RecommendationDto> Recommendations,
    DateTimeOffset CreatedAt);
