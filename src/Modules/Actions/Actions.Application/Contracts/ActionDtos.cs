using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Actions.Application.Contracts;

// Read models returned to the SPA. Enums project as their string names (stable wire contract, localized
// in the UI). Bilingual text is the LocalizedString value object (the SPA picks the locale). Actions never
// joins another module's tables (ADR-0001) — only ids + display-key snapshots travel. IsOverdue is the
// DERIVED overlay computed against the request clock (docs/domain/entity-lifecycles.md line 159), not a stored status.

public sealed record ActionSummaryDto(
    Guid Id,
    string Key,
    LocalizedString Title,
    string Status,
    string Priority,
    string OwnerUserId,
    string OwnerName,
    DateTimeOffset? DueDate,
    bool IsOverdue,
    int ProgressPct,
    string SourceType,
    Guid SourceId,
    string? SourceKey,
    string? MeetingKey);

public sealed record ActionDetailDto(
    Guid Id,
    string Key,
    LocalizedString Title,
    LocalizedString? Description,
    string Status,
    string Priority,
    string OwnerUserId,
    string OwnerName,
    DateTimeOffset? DueDate,
    bool IsOverdue,
    int ProgressPct,
    string SourceType,
    Guid SourceId,
    string? SourceKey,
    string? MeetingKey,
    LocalizedString? BlockedReason,
    LocalizedString? CompletionNote,
    LocalizedString? CancelReason,
    string? CompletedByUserId,
    DateTimeOffset? CompletedAt,
    string? VerifiedByUserId,
    string? VerifiedByName,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset CreatedAt);
