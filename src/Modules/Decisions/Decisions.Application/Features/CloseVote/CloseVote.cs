using Acmp.Modules.Decisions.Application.Abstractions;
using Acmp.Modules.Decisions.Application.Internal;
using Acmp.Modules.Decisions.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Decisions.Application.Features.CloseVote;

// W11 (close): the Secretary (or Chairman) closes voting (Open → Closed). RBAC = Vote.Manage. AC-024: the
// cast quorum (non-recused ballots ≥ MinCast) is enforced by the domain — a shortfall keeps the vote Open.
// The tally is computed and FROZEN, and the closing actor is recorded as the counter of record (SoD-3,
// Option A — checked later on the decision-issue path, AC-015/016). Audited high-importance (tally frozen).
public sealed record CloseVoteCommand(Guid VoteId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Chairman, AcmpRoles.Secretary };
}

public sealed class CloseVoteValidator : AbstractValidator<CloseVoteCommand>
{
    public CloseVoteValidator() => RuleFor(x => x.VoteId).NotEmpty();
}

public sealed class CloseVoteHandler : IRequestHandler<CloseVoteCommand>
{
    private readonly IDecisionsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public CloseVoteHandler(IDecisionsDbContext db, ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task Handle(CloseVoteCommand request, CancellationToken ct)
    {
        var vote = await _db.Votes.FirstOrDefaultAsync(v => v.PublicId == request.VoteId, ct)
            ?? throw new KeyNotFoundException("Vote not found.");

        var (sub, name) = CurrentActor.Of(_user);
        vote.Close(sub, name, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Decisions.VoteClosed", nameof(Vote), vote.PublicId.ToString(), ct: ct);
    }
}
