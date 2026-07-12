using Acmp.Modules.Research.Application.Abstractions;
using Acmp.Modules.Research.Application.Contracts;
using Acmp.Modules.Research.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Research.Application.Features.GetMissionByKey;

// Mission detail by display key (RMS-YYYY-###): the mission fields plus its owned findings + recommendations
// (in-module lookups over the research schema — no cross-module read). Readable by any authenticated committee
// member (read-all).
public sealed record GetMissionByKeyQuery(string Key) : IRequest<ResearchMissionDetailDto?>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetMissionByKeyHandler : IRequestHandler<GetMissionByKeyQuery, ResearchMissionDetailDto?>
{
    private readonly IResearchDbContext _db;

    public GetMissionByKeyHandler(IResearchDbContext db) => _db = db;

    public async Task<ResearchMissionDetailDto?> Handle(GetMissionByKeyQuery request, CancellationToken ct)
    {
        var mission = await _db.Missions.AsNoTracking()
            .Include(m => m.Findings)
            .Include(m => m.Recommendations)
            .FirstOrDefaultAsync(m => m.Key == request.Key, ct);
        return mission is null ? null : ResearchMapping.ToDetail(mission);
    }
}
