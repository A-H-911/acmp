using Acmp.Modules.Decisions.Application.Abstractions;
using Acmp.Shared.Contracts.Decisions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Decisions.Infrastructure.Directory;

// IDecisionReader impl (P11e) — the same read the /api/decisions detail uses, projected to the lean
// promotion shape. Never mutates; the promotion decision (eligibility, idempotency) is the caller's.
public sealed class DecisionReader : IDecisionReader
{
    private readonly IDecisionsDbContext _db;

    public DecisionReader(IDecisionsDbContext db) => _db = db;

    public async Task<DecisionForPromotion?> GetForPromotionAsync(Guid id, CancellationToken ct = default)
    {
        var d = await _db.Decisions.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == id, ct);
        return d is null
            ? null
            : new DecisionForPromotion(d.PublicId, d.Key, d.Status.ToString(), d.Title, d.Statement, d.Rationale, d.Alternatives);
    }
}
