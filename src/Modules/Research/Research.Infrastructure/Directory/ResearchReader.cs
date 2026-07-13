using Acmp.Modules.Research.Application.Abstractions;
using Acmp.Shared.Contracts.Research;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Research.Infrastructure.Directory;

// IResearchReader impl (P15c / W16) — the lean projections the Topics convert reads, over this module's own
// store. Never mutates; convert eligibility + idempotency are the caller's. Recommendations are owned children
// of the mission, so EF loads them with the aggregate — no explicit Include (which owned types reject).
public sealed class ResearchReader : IResearchReader
{
    private readonly IResearchDbContext _db;

    public ResearchReader(IResearchDbContext db) => _db = db;

    public async Task<MissionForConvert?> GetMissionForConvertAsync(Guid missionId, CancellationToken ct = default)
    {
        var m = await _db.Missions.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == missionId, ct);
        return m is null ? null : new MissionForConvert(m.PublicId, m.Key, m.Title.En, m.Status.ToString());
    }

    public async Task<RecommendationForConvert?> GetRecommendationForConvertAsync(
        Guid missionId, Guid recommendationId, CancellationToken ct = default)
    {
        var m = await _db.Missions.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == missionId, ct);
        var r = m?.Recommendations.FirstOrDefault(x => x.PublicId == recommendationId);
        return r is null
            ? null
            : new RecommendationForConvert(r.PublicId, r.Key, r.Statement.En, r.Status.ToString(),
                m!.PublicId, m.Key, r.LinkedTopicId);
    }
}
