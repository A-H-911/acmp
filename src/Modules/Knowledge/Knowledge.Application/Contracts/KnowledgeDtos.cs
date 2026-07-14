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

// Template read models (P15d-2, FR-119). Name is bilingual; Body is a single Markdown string. TargetType + Status
// project as their string names. The register row omits Body (the register lists templates; the detail carries
// the content).
public sealed record TemplateSummaryDto(
    Guid Id,
    string Key,
    LocalizedString Name,
    string TargetType,
    string Status,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record TemplateDetailDto(
    Guid Id,
    string Key,
    LocalizedString Name,
    string TargetType,
    string Body,
    string Status,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
