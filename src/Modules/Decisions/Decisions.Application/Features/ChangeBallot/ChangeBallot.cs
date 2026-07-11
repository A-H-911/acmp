using Acmp.Modules.Decisions.Application.Abstractions;
using Acmp.Modules.Decisions.Application.Internal;
using Acmp.Modules.Decisions.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Decisions.Application.Features.ChangeBallot;

// W11 (change): the design allows "you can change your vote until voting closes" — overwrite the current
// voter's ballot while the vote is Open. RBAC = Vote.Cast (Chairman/Member); the voter is the current user.
// Distinct from CastBallot (first-submission, AC-022 double-vote reject) so both rules coexist.
public sealed record ChangeBallotCommand(Guid VoteId, string Choice, LocalizedString? Comment)
    : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Chairman, AcmpRoles.Member };
}

public sealed class ChangeBallotValidator : AbstractValidator<ChangeBallotCommand>
{
    public ChangeBallotValidator()
    {
        RuleFor(x => x.VoteId).NotEmpty();
        RuleFor(x => x.Choice).NotEmpty().WithMessage("A choice is required.");
    }
}

public sealed class ChangeBallotHandler : IRequestHandler<ChangeBallotCommand>
{
    private readonly IDecisionsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public ChangeBallotHandler(IDecisionsDbContext db, ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(ChangeBallotCommand request, CancellationToken ct)
    {
        var vote = await _db.Votes.FirstOrDefaultAsync(v => v.PublicId == request.VoteId, ct)
            ?? throw new KeyNotFoundException("Vote not found.");

        var (sub, _) = CurrentActor.Of(_user);
        vote.ChangeBallot(sub, request.Choice, request.Comment, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Decisions.BallotChanged", nameof(Vote), vote.PublicId.ToString(), ct: ct);
    }
}
