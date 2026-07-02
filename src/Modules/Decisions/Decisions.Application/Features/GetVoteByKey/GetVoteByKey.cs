using Acmp.Modules.Decisions.Application.Abstractions;
using Acmp.Modules.Decisions.Application.Contracts;
using Acmp.Modules.Decisions.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Decisions.Application.Features.GetVoteByKey;

// Vote detail by display key (VOTE-YYYY-###): status, options, quorum, the frozen tally, and every
// attributed ballot (AC-023 — no voter is masked). Readable by any authenticated committee member (read-all).
public sealed record GetVoteByKeyQuery(string Key) : IRequest<VoteDetailDto?>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetVoteByKeyHandler : IRequestHandler<GetVoteByKeyQuery, VoteDetailDto?>
{
    private readonly IDecisionsDbContext _db;

    public GetVoteByKeyHandler(IDecisionsDbContext db) => _db = db;

    public async Task<VoteDetailDto?> Handle(GetVoteByKeyQuery request, CancellationToken ct)
    {
        var vote = await _db.Votes.AsNoTracking().FirstOrDefaultAsync(v => v.Key == request.Key, ct);
        return vote is null ? null : VoteMapping.ToDetail(vote);
    }
}
