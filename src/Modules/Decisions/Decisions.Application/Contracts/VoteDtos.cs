using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Decisions.Application.Contracts;

// Read models returned to the SPA. Enums project as string names (stable wire contract). Ballots are
// always attributed (ADR-0010 — no anonymity): each row carries the voter + their choice. Votes never join
// Topics/Meetings tables (ADR-0001) — only ids travel.

public sealed record BallotDto(
    string VoterUserId,
    string VoterName,
    string? Choice,
    LocalizedString? Comment,
    bool Recused,
    DateTimeOffset? CastAt);

public sealed record VoteTallyDto(
    IReadOnlyDictionary<string, int> OptionCounts,
    int AbstainCount,
    int CastCount);

public sealed record VoteSummaryDto(
    Guid Id,
    string Key,
    Guid TopicId,
    Guid? MeetingId,
    string Status,
    IReadOnlyList<string> Options,
    bool AllowAbstain,
    int MinPresent,
    int MinCast,
    DateTimeOffset? OpenedAt,
    DateTimeOffset? ClosedAt);

public sealed record VoteDetailDto(
    Guid Id,
    string Key,
    Guid TopicId,
    Guid? MeetingId,
    string Status,
    IReadOnlyList<string> Options,
    bool AllowAbstain,
    int MinPresent,
    int MinCast,
    VoteTallyDto? Tally,
    string? ResultSummary,
    DateTimeOffset? OpenedAt,
    DateTimeOffset? ClosedAt,
    string? CounterUserId,
    string? CounterName,
    IReadOnlyList<BallotDto> Ballots);

// Request shape for an eligible voter on the configure command (sub + display-name snapshot).
public sealed record VoteEligibleVoterRequest(string UserId, string Name);
