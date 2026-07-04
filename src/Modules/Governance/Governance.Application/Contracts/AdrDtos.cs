using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Governance.Application.Contracts;

// Read models returned to the SPA. Enums project as their string names (stable wire contract, localized in
// the UI). Bilingual text is the LocalizedString value object (the SPA picks the locale). ADRs never join
// another module's tables (ADR-0001); the supersession links resolve peer ADR keys IN-module for the detail
// banners. The register row is deliberately lean (the isAdrs list columns); the full MADR body is the detail.

public sealed record AdrOptionDto(Guid Id, LocalizedString Name, LocalizedString? Body, bool IsChosen);

public sealed record AdrSummaryDto(
    Guid Id,
    string Key,
    LocalizedString Title,
    string Status,
    string AuthorName,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset CreatedAt,
    bool IsSuperseded);

public sealed record AdrDetailDto(
    Guid Id,
    string Key,
    LocalizedString Title,
    string Status,
    LocalizedString Context,
    LocalizedString? DecisionDrivers,
    LocalizedString DecisionText,
    LocalizedString? ConsequencesPositive,
    LocalizedString? ConsequencesNegative,
    IReadOnlyList<AdrOptionDto> Options,
    string AuthorUserId,
    string AuthorName,
    Guid? SourceDecisionId,
    DateTimeOffset? ApprovedAt,
    string? ApprovedByName,
    Guid? SupersededByAdrId,
    string? SupersededByAdrKey,
    LocalizedString? SupersessionReason,
    Guid? SupersedesAdrId,
    string? SupersedesAdrKey,
    LocalizedString? DeprecationReason,
    DateTimeOffset CreatedAt);
