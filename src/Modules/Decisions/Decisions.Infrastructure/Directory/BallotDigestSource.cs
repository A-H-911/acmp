using Acmp.Modules.Decisions.Application.Abstractions;
using Acmp.Modules.Decisions.Domain.Enums;
using Acmp.Shared.Contracts.Decisions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Decisions.Infrastructure.Directory;

// Decisions-owned implementation of IBallotDigestSource (ADR-0001) for the digest (AC-161): open votes on which
// the member holds a ballot that is neither cast (Choice set) nor recused.
public sealed class BallotDigestSource : IBallotDigestSource
{
    private readonly IDecisionsDbContext _db;

    public BallotDigestSource(IDecisionsDbContext db) => _db = db;

    public Task<int> CountAwaitingAsync(string voterUserId, CancellationToken ct = default) =>
        _db.Votes.AsNoTracking()
            .CountAsync(v => v.Status == VoteStatus.Open &&
                v.Ballots.Any(b => b.VoterUserId == voterUserId && b.Choice == null && !b.Recused), ct);
}
