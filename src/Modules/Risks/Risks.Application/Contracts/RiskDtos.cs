using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Risks.Application.Contracts;

// Read models returned to the SPA. Enums project as their string names (stable wire contract, localized in
// the UI). Bilingual text is the LocalizedString value object (the SPA picks the locale). Risks never join
// another module's tables (ADR-0001) — only ids + a display-key snapshot travel. Severity (1..9) + the
// Exposure band are the DERIVED overlay (docs/domain/entity-lifecycles.md line 247), computed here, never stored; the design's heat
// grid consumes the band as-is.

public sealed record RiskSummaryDto(
    Guid Id,
    string Key,
    LocalizedString Title,
    string Status,
    string Likelihood,
    string Impact,
    int Severity,
    string Exposure,
    string OwnerUserId,
    string OwnerName,
    string SubjectType,
    Guid SubjectId,
    string? SubjectKey);

public sealed record MitigationDto(
    Guid Id,
    LocalizedString Description,
    string Type,
    string Status,
    string? OwnerUserId,
    Guid? LinkedActionId,
    DateTimeOffset? DueDate);

public sealed record RiskDetailDto(
    Guid Id,
    string Key,
    LocalizedString Title,
    LocalizedString? Description,
    string Status,
    string Likelihood,
    string Impact,
    int Severity,
    string Exposure,
    string OwnerUserId,
    string OwnerName,
    string SubjectType,
    Guid SubjectId,
    string? SubjectKey,
    IReadOnlyList<MitigationDto> Mitigations,
    LocalizedString? ClosureNote,
    LocalizedString? AcceptanceRationale,
    string? AcceptingAuthority,
    LocalizedString? EscalationReason,
    string? EscalationTarget,
    DateTimeOffset? ClosedAt,
    DateTimeOffset CreatedAt);
