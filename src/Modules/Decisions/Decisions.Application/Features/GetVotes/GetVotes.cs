using Acmp.Modules.Decisions.Application.Abstractions;
using Acmp.Modules.Decisions.Application.Contracts;
using Acmp.Modules.Decisions.Application.Internal;
using Acmp.Modules.Decisions.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Decisions.Application.Features.GetVotes;

// Committee-wide votes register (across every topic), newest first. Feeds the chairman dashboard's
// "votes awaiting your approval" queue — Closed but not yet Ratified (AC-066) — and the Reports
// voting view. Optional status filter (VoteStatus name; unparseable = all). Read-all: any
// authenticated committee member. Reads only Decisions' own Votes table (ADR-0001).
public sealed record GetVotesQuery(string? Status)
    : IRequest<IReadOnlyList<VoteSummaryDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetVotesHandler : IRequestHandler<GetVotesQuery, IReadOnlyList<VoteSummaryDto>>
{
    private readonly IDecisionsDbContext _db;

    public GetVotesHandler(IDecisionsDbContext db) => _db = db;

    public async Task<IReadOnlyList<VoteSummaryDto>> Handle(GetVotesQuery request, CancellationToken ct)
    {
        var query = _db.Votes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<VoteStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(v => v.Status == status);
        }

        var votes = await query
            .OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.Id)
            .ToListAsync(ct);

        return votes.Select(VoteMapping.ToSummary).ToList();
    }
}
