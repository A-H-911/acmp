namespace Acmp.Shared.Contracts.Research;

// Cross-module read seam (ADR-0001, P15c / W16): the Topics module reads a research mission or one of its
// recommendations to convert it into an execution Topic, without ever touching the Research module's tables.
// Implemented in Research.Infrastructure over the same store the /api/research detail reads. Lean projections
// (not the full detail DTO): only what the convert needs — key + EN title/statement snapshot for the
// traceability edge, status for the eligibility guard, and (for a recommendation) its parent mission + any
// existing LinkedTopicId. Bilingual text is flattened to its EN value here (the edge snapshot is a single
// string; the SPA supplies the new topic's own text). The Research enums never leak — Status travels as its
// string name.

public sealed record MissionForConvert(Guid Id, string Key, string TitleEn, string Status);

public sealed record RecommendationForConvert(
    Guid Id, string Key, string StatementEn, string Status,
    Guid MissionId, string MissionKey, Guid? LinkedTopicId);

public interface IResearchReader
{
    // Returns null when the mission does not exist (the caller maps that to 404).
    Task<MissionForConvert?> GetMissionForConvertAsync(Guid missionId, CancellationToken ct = default);

    // Returns null when the mission or the recommendation under it does not exist.
    Task<RecommendationForConvert?> GetRecommendationForConvertAsync(
        Guid missionId, Guid recommendationId, CancellationToken ct = default);
}
