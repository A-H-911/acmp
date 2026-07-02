using Acmp.Modules.Decisions.Application.Abstractions;
using Acmp.Modules.Decisions.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Decisions.Application.Features.CastBallot;

// W11 (cast): an eligible voter casts their first ballot on an Open vote. RBAC = Vote.Cast (Chairman/Member);
// the voter is the current user. AC-022: a second cast is REJECTED — the denied attempt is audited before the
// refusal (mirrors the SoD-1 verify-denial pattern), and the first ballot is left unchanged. Use ChangeBallot
// to change a vote while the vote is still Open.
public sealed record CastBallotCommand(Guid VoteId, string Choice, LocalizedString? Comment)
    : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Chairman, AcmpRoles.Member };
}

public sealed class CastBallotValidator : AbstractValidator<CastBallotCommand>
{
    public CastBallotValidator()
    {
        RuleFor(x => x.VoteId).NotEmpty();
        RuleFor(x => x.Choice).NotEmpty().WithMessage("A choice is required.");
    }
}

public sealed class CastBallotHandler : IRequestHandler<CastBallotCommand>
{
    private readonly IDecisionsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public CastBallotHandler(IDecisionsDbContext db, ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(CastBallotCommand request, CancellationToken ct)
    {
        var vote = await _db.Votes.FirstOrDefaultAsync(v => v.PublicId == request.VoteId, ct)
            ?? throw new KeyNotFoundException("Vote not found.");

        var (sub, _) = CurrentActor.Of(_user);

        // AC-022: audit the denied double-vote, then refuse (the domain re-guards as defence in depth).
        var existing = vote.Ballots.FirstOrDefault(b =>
            string.Equals(b.VoterUserId, sub, StringComparison.Ordinal));
        if (existing is { HasCast: true })
        {
            await _audit.EmitAsync("Decisions.BallotDenied", sub,
                new { vote.PublicId, vote.Key, Reason = "AC-022: voter has already cast a ballot" }, ct);
            throw new InvalidOperationException("You have already voted.");
        }

        vote.Cast(sub, request.Choice, request.Comment, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitAsync("Decisions.BallotCast", sub, new { vote.PublicId, vote.Key }, ct);
    }
}
