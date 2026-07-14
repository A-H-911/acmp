using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Knowledge.Application.Contracts;

// Read models returned to the SPA. Enums project as their string names (stable wire contract, localized in the
// UI). Bilingual text is the LocalizedString value object (the SPA picks the locale). The register row is lean;
// the full document (incl. its version history) is the detail.

public sealed record DocumentVersionDto(
    Guid Id,
    int Version,
    LocalizedString Title,
    LocalizedString Body,
    DateTimeOffset SavedAt,
    string SavedByUserId);

public sealed record DocumentSummaryDto(
    Guid Id,
    string Key,
    LocalizedString Title,
    string Status,
    string Category,
    IReadOnlyList<string> Tags,
    string OwnerUserId,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record DocumentDetailDto(
    Guid Id,
    string Key,
    LocalizedString Title,
    LocalizedString Body,
    string Status,
    string Category,
    IReadOnlyList<string> Tags,
    string OwnerUserId,
    int Version,
    IReadOnlyList<DocumentVersionDto> Versions,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
