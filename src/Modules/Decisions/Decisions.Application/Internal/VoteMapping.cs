using Acmp.Modules.Decisions.Application.Contracts;
using Acmp.Modules.Decisions.Domain;

namespace Acmp.Modules.Decisions.Application.Internal;

// Aggregate → read-model projection, shared by the queries and the command return values.
internal static class VoteMapping
{
    public static BallotDto ToDto(Ballot b) => new(
        b.VoterUserId, b.VoterName, b.Choice, b.Comment, b.Recused, b.CastAt);

    public static VoteTallyDto? ToDto(VoteTally? t) => t is null
        ? null
        : new VoteTallyDto(t.OptionCounts, t.AbstainCount, t.CastCount);

    public static VoteSummaryDto ToSummary(Vote v) => new(
        v.PublicId, v.Key, v.TopicId, v.MeetingId, v.Status.ToString(),
        v.Options, v.AllowAbstain, v.QuorumRule.MinPresent, v.QuorumRule.MinCast, v.OpenedAt, v.ClosedAt);

    public static VoteDetailDto ToDetail(Vote v) => new(
        v.PublicId, v.Key, v.TopicId, v.MeetingId, v.Status.ToString(),
        v.Options, v.AllowAbstain, v.QuorumRule.MinPresent, v.QuorumRule.MinCast,
        ToDto(v.Tally), v.ResultSummary, v.OpenedAt, v.ClosedAt, v.CounterUserId, v.CounterName,
        v.Ballots.Select(ToDto).ToList());
}
