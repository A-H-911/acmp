using Acmp.Modules.Risks.Application.Abstractions;
using Acmp.Modules.Risks.Application.Contracts;
using Acmp.Modules.Risks.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Risks.Application.Features.GetRiskByKey;

// Risk detail by display key (RSK-YYYY-###): likelihood/impact + derived exposure, owner, subject link, the
// mitigations, and the acceptance/escalation/closure evidence. Readable by any authenticated committee
// member (read-all). Owned mitigations are always loaded with the aggregate.
public sealed record GetRiskByKeyQuery(string Key) : IRequest<RiskDetailDto?>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetRiskByKeyHandler : IRequestHandler<GetRiskByKeyQuery, RiskDetailDto?>
{
    private readonly IRisksDbContext _db;

    public GetRiskByKeyHandler(IRisksDbContext db) => _db = db;

    public async Task<RiskDetailDto?> Handle(GetRiskByKeyQuery request, CancellationToken ct)
    {
        var risk = await _db.Risks.AsNoTracking().FirstOrDefaultAsync(r => r.Key == request.Key, ct);
        return risk is null ? null : RiskMapping.ToDetail(risk);
    }
}
