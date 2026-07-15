using Acmp.Modules.Research.Application.Contracts;
using Acmp.Modules.Research.Domain;

namespace Acmp.Modules.Research.Application.Internal;

// Aggregate → read-model projection, shared by the queries and the command return values. All lookups are
// in-module (research schema only) — a mission never joins another module's tables (ADR-0001).
internal static class ResearchMapping
{
    public static ResearchMissionSummaryDto ToSummary(ResearchMission m) => new(
        m.PublicId, m.Key, m.Title, m.Status.ToString(), m.OwnerName, m.CreatedAt, m.UpdatedAt,
        m.Findings.Count, m.Recommendations.Count);

    public static ResearchMissionDetailDto ToDetail(ResearchMission m) => new(
        m.PublicId, m.Key, m.Title, m.Question, m.Status.ToString(),
        m.OwnerUserId, m.OwnerName, m.KeystonePackageRef, m.SourceTopicId, m.CompletedAt, m.CancellationReason,
        m.Findings.Select(ToFinding).ToList(),
        m.Recommendations.Select(ToRecommendation).ToList(),
        m.CreatedAt);

    private static FindingDto ToFinding(Finding f) =>
        new(f.PublicId, f.Key, f.Summary, f.Detail, f.Confidence.ToString(), f.IsVerified);

    private static RecommendationDto ToRecommendation(Recommendation r) =>
        new(r.PublicId, r.Key, r.Statement, r.Rationale, r.Priority.ToString(), r.Status.ToString(), r.LinkedTopicId);
}
