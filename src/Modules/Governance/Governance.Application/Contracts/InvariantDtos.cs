using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Governance.Application.Contracts;

// Read models returned to the SPA. Enums project as their string names (stable wire contract, localized in
// the UI). Bilingual text is the LocalizedString value object (the SPA picks the locale). Invariants never
// join another module's tables (ADR-0001); the supersession links resolve peer invariant keys IN-module for
// the detail banners. The register row is deliberately lean (the isAdrs invariants-tab list columns); the
// full statement/rationale body is the detail.

public sealed record InvariantSummaryDto(
    Guid Id,
    string Key,
    LocalizedString Statement,
    string Status,
    string Category,
    string Scope,
    string OwnerName,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset CreatedAt,
    bool IsSuperseded);

public sealed record InvariantDetailDto(
    Guid Id,
    string Key,
    string Status,
    string Category,
    string Scope,
    LocalizedString Statement,
    LocalizedString Rationale,
    LocalizedString? ExceptionsPolicy,
    string OwnerUserId,
    string OwnerName,
    DateTimeOffset? ActivatedAt,
    string? ActivatedByName,
    Guid? SupersededByInvariantId,
    string? SupersededByInvariantKey,
    LocalizedString? SupersessionReason,
    Guid? SupersedesInvariantId,
    string? SupersedesInvariantKey,
    LocalizedString? RetirementReason,
    DateTimeOffset CreatedAt);
