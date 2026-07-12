using Acmp.Modules.Decisions.Application.Abstractions;
using Acmp.Modules.Decisions.Application.Internal;
using Acmp.Modules.Decisions.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Decisions.Application.Features.RecuseVote;

// W11 (recuse): the current voter recuses from an Open vote — their ballot is excluded from the quorum base
// and the tally (a distinct state, not a choice; docs/domain/domain-model.md §Vote). RBAC = Vote.Cast (Chairman/Member).
public sealed record RecuseVoteCommand(Guid VoteId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Chairman, AcmpRoles.Member };
}

public sealed class RecuseVoteValidator : AbstractValidator<RecuseVoteCommand>
{
    public RecuseVoteValidator() => RuleFor(x => x.VoteId).NotEmpty();
}

public sealed class RecuseVoteHandler : IRequestHandler<RecuseVoteCommand>
{
    private readonly IDecisionsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public RecuseVoteHandler(IDecisionsDbContext db, ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(RecuseVoteCommand request, CancellationToken ct)
    {
        var vote = await _db.Votes.FirstOrDefaultAsync(v => v.PublicId == request.VoteId, ct)
            ?? throw new KeyNotFoundException("Vote not found.");

        var (sub, _) = CurrentActor.Of(_user);
        vote.Recuse(sub, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Decisions.VoteRecused", nameof(Vote), vote.PublicId.ToString(), ct: ct);
    }
}
