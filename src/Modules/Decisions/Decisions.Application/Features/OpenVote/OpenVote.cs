using Acmp.Modules.Decisions.Application.Abstractions;
using Acmp.Modules.Decisions.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Meetings;
using Acmp.Shared.Contracts.Notifications;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Decisions.Application.Features.OpenVote;

// W11 (open): the Secretary (or Chairman) opens voting (Configured → Open). RBAC = Vote.Manage. The
// present-quorum gate (docs/12 §4) resolves the eligible-and-present count from the linked meeting via the
// Meetings seam (ADR-0001) and lets the domain compare it to MinPresent. On success: lock the config
// (AC-021), fan out a VoteOpened notification with a /votes/{key} deep link to each eligible voter
// (AC-021/AC-052), and audit.
//
// ponytail: when the vote has no linked meeting, there is no attendance to count — the present check is
// skipped by passing MinPresent (the gate trivially holds). Flagged as an assumption.
public sealed record OpenVoteCommand(Guid VoteId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Chairman, AcmpRoles.Secretary };
}

public sealed class OpenVoteValidator : AbstractValidator<OpenVoteCommand>
{
    public OpenVoteValidator() => RuleFor(x => x.VoteId).NotEmpty();
}

public sealed class OpenVoteHandler : IRequestHandler<OpenVoteCommand>
{
    private readonly IDecisionsDbContext _db;
    private readonly IMeetingQuorumSource _quorum;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly INotificationChannel _notifications;

    public OpenVoteHandler(IDecisionsDbContext db, IMeetingQuorumSource quorum, ICurrentUser user,
        IClock clock, IAuditSink audit, INotificationChannel notifications)
    {
        _db = db;
        _quorum = quorum;
        _user = user;
        _clock = clock;
        _audit = audit;
        _notifications = notifications;
    }

    public async Task Handle(OpenVoteCommand request, CancellationToken ct)
    {
        var vote = await _db.Votes.FirstOrDefaultAsync(v => v.PublicId == request.VoteId, ct)
            ?? throw new KeyNotFoundException("Vote not found.");

        var (sub, _) = CurrentActor.Of(_user);

        // Live attendance-linked present quorum: count eligible-and-present in the linked meeting; when there
        // is no linked meeting, skip the present check (pass MinPresent so the gate holds).
        var present = vote.MeetingId is { } meetingId
            ? await _quorum.GetPresentEligibleCountAsync(meetingId, ct)
            : vote.QuorumRule.MinPresent;

        vote.Open(sub, present, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        var recipients = vote.Ballots.Select(b => b.VoterUserId).ToList();
        await VoteNotifications.FanOutAsync(_notifications, recipients, VoteNotifications.VoteOpened(vote.Key), ct);
        await _audit.EmitAsync("Decisions.VoteOpened", sub, new { vote.PublicId, vote.Key, Present = present }, ct);
    }
}
