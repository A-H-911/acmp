using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Shared.Contracts.Decisions;

// Cross-module read seam (ADR-0001, P11e): Governance's Decision→ADR promotion (FR-068) reads a decision's
// content to pre-fill the new ADR, without ever touching the Decisions module's tables. Implemented in
// Decisions.Infrastructure over the same store the /api/decisions detail reads. Bilingual text travels as the
// shared LocalizedString value object; the Decisions enums never leak — Status travels as its string name.

// The decision content the promotion needs (a lean projection, not the full detail DTO).
public sealed record DecisionForPromotion(
    Guid Id,
    string Key,
    string Status,
    LocalizedString Title,
    LocalizedString Statement,
    LocalizedString Rationale,
    LocalizedString? Alternatives);

public interface IDecisionReader
{
    // Returns null when the decision does not exist (the caller maps that to 404).
    Task<DecisionForPromotion?> GetForPromotionAsync(Guid id, CancellationToken ct = default);
}
