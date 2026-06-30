using Acmp.Modules.Decisions.Application.Abstractions;
using Acmp.Modules.Decisions.Application.Contracts;
using Acmp.Modules.Decisions.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Decisions.Application.Features.GetDecisionsByTopic;

// All decisions recorded against one topic (its decision history — the original plus any supersessions),
// newest first. Filtered by Topic.PublicId (a Guid the SPA already holds): Decisions stores only the
// topic id and must NOT resolve a TOP- key, which would require reading Topics' tables (ADR-0001).
// Readable by any authenticated committee member (read-all).
public sealed record GetDecisionsByTopicQuery(Guid TopicId) : IRequest<IReadOnlyList<DecisionSummaryDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetDecisionsByTopicHandler : IRequestHandler<GetDecisionsByTopicQuery, IReadOnlyList<DecisionSummaryDto>>
{
    private readonly IDecisionsDbContext _db;

    public GetDecisionsByTopicHandler(IDecisionsDbContext db) => _db = db;

    public async Task<IReadOnlyList<DecisionSummaryDto>> Handle(GetDecisionsByTopicQuery request, CancellationToken ct)
    {
        var decisions = await _db.Decisions.AsNoTracking()
            .Where(d => d.TopicId == request.TopicId)
            .OrderByDescending(d => d.Id)
            .ToListAsync(ct);

        return decisions.Select(DecisionMapping.ToSummary).ToList();
    }
}
