using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Decisions.Application.Contracts;

// Read models returned to the SPA. Enums project as their string names (stable wire contract, localized
// in the UI). Bilingual text is returned as the LocalizedString value object (the SPA picks the locale).
// Decisions never joins Topics/Meetings tables (ADR-0001) — only ids travel.

public sealed record DecisionConditionDto(
    Guid Id,
    LocalizedString Text,
    string Status,
    DateTimeOffset? DueDate,
    Guid? LinkedActionId);

public sealed record DecisionSummaryDto(
    Guid Id,
    string Key,
    Guid TopicId,
    Guid? MeetingId,
    string Outcome,
    string Status,
    LocalizedString Title,
    DateTimeOffset? IssuedAt);

public sealed record DecisionDetailDto(
    Guid Id,
    string Key,
    Guid TopicId,
    Guid? MeetingId,
    string Outcome,
    string Status,
    LocalizedString Title,
    LocalizedString Rationale,
    LocalizedString? Alternatives,
    Guid? VoteId,
    string? ChairApprovedByUserId,
    string? ChairApprovedByName,
    bool ChairOverride,
    LocalizedString? OverrideJustification,
    DateTimeOffset? IssuedAt,
    Guid? SupersededByDecisionId,
    LocalizedString? SupersessionReason,
    IReadOnlyList<DecisionConditionDto> Conditions);

// Request shape for a condition on the record/supersede commands.
public sealed record DecisionConditionRequest(LocalizedString Text, DateTimeOffset? DueDate);
