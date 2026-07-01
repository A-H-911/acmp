using Acmp.Modules.Decisions.Application.Contracts;
using Acmp.Modules.Decisions.Domain;

namespace Acmp.Modules.Decisions.Application.Internal;

// Aggregate → read-model projection, shared by the queries and the command return values.
internal static class DecisionMapping
{
    public static DecisionConditionDto ToDto(DecisionCondition c) => new(
        c.PublicId, c.Text, c.Status.ToString(), c.DueDate, c.LinkedActionId);

    public static DecisionSummaryDto ToSummary(Decision d) => new(
        d.PublicId, d.Key, d.TopicId, d.MeetingId, d.Outcome.ToString(), d.Status.ToString(), d.Title, d.IssuedAt);

    public static DecisionDetailDto ToDetail(Decision d) => new(
        d.PublicId, d.Key, d.TopicId, d.MeetingId, d.Outcome.ToString(), d.Status.ToString(),
        d.Title, d.Rationale, d.Alternatives, d.VoteId,
        d.ChairApprovedByUserId, d.ChairApprovedByName, d.ChairOverride, d.OverrideJustification,
        d.IssuedAt, d.SupersededByDecisionId, d.SupersessionReason,
        d.Conditions.Select(ToDto).ToList());
}
