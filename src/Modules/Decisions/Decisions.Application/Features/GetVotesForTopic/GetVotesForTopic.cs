using Acmp.Modules.Decisions.Application.Abstractions;
using Acmp.Modules.Decisions.Application.Contracts;
using Acmp.Modules.Decisions.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Decisions.Application.Features.GetVotesForTopic;

// All votes run against one topic (its ballot history), newest first. Filtered by Topic.PublicId (a Guid the
// SPA already holds): Votes stores only the topic id and must NOT resolve a TOP- key, which would require
// reading Topics' tables (ADR-0001). Readable by any authenticated committee member (read-all).
public sealed record GetVotesForTopicQuery(Guid TopicId) : IRequest<IReadOnlyList<VoteSummaryDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetVotesForTopicHandler : IRequestHandler<GetVotesForTopicQuery, IReadOnlyList<VoteSummaryDto>>
{
    private readonly IDecisionsDbContext _db;

    public GetVotesForTopicHandler(IDecisionsDbContext db) => _db = db;

    public async Task<IReadOnlyList<VoteSummaryDto>> Handle(GetVotesForTopicQuery request, CancellationToken ct)
    {
        var votes = await _db.Votes.AsNoTracking()
            .Where(v => v.TopicId == request.TopicId)
            .OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.Id)
            .ToListAsync(ct);

        return votes.Select(VoteMapping.ToSummary).ToList();
    }
}
