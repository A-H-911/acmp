using Acmp.Modules.Decisions.Application.Abstractions;
using Acmp.Modules.Decisions.Application.Contracts;
using Acmp.Modules.Decisions.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Decisions.Application.Features.GetDecisionByKey;

// Decision detail by display key (DECN-YYYY-###): outcome, rationale, alternatives, conditions, chair
// attribution, and the supersession back-link. Readable by any authenticated committee member (read-all).
public sealed record GetDecisionByKeyQuery(string Key) : IRequest<DecisionDetailDto?>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetDecisionByKeyHandler : IRequestHandler<GetDecisionByKeyQuery, DecisionDetailDto?>
{
    private readonly IDecisionsDbContext _db;

    public GetDecisionByKeyHandler(IDecisionsDbContext db) => _db = db;

    public async Task<DecisionDetailDto?> Handle(GetDecisionByKeyQuery request, CancellationToken ct)
    {
        var decision = await _db.Decisions.AsNoTracking().FirstOrDefaultAsync(d => d.Key == request.Key, ct);
        return decision is null ? null : DecisionMapping.ToDetail(decision);
    }
}
