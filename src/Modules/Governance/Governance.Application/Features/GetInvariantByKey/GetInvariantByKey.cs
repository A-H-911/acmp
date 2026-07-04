using Acmp.Modules.Governance.Application.Abstractions;
using Acmp.Modules.Governance.Application.Contracts;
using Acmp.Modules.Governance.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Governance.Application.Features.GetInvariantByKey;

// Invariant detail by display key (AIV-YYYY-###): the full statement/rationale/exceptions body, activation
// attribution, and the supersession links resolved to peer invariant keys for the detail banners (in-module
// lookups over the governance schema — no cross-module read). Readable by any authenticated committee member.
public sealed record GetInvariantByKeyQuery(string Key) : IRequest<InvariantDetailDto?>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetInvariantByKeyHandler : IRequestHandler<GetInvariantByKeyQuery, InvariantDetailDto?>
{
    private readonly IGovernanceDbContext _db;

    public GetInvariantByKeyHandler(IGovernanceDbContext db) => _db = db;

    public async Task<InvariantDetailDto?> Handle(GetInvariantByKeyQuery request, CancellationToken ct)
    {
        var inv = await _db.Invariants.AsNoTracking().FirstOrDefaultAsync(a => a.Key == request.Key, ct);
        if (inv is null) return null;

        // Resolve the peer invariant keys for the supersedes / superseded-by banners (in-module).
        var supersededByKey = await KeyOf(inv.SupersededByInvariantId, ct);
        var supersedesKey = await KeyOf(inv.SupersedesInvariantId, ct);
        return InvariantMapping.ToDetail(inv, supersededByKey, supersedesKey);
    }

    private async Task<string?> KeyOf(Guid? invariantId, CancellationToken ct)
    {
        if (invariantId is null) return null;
        return await _db.Invariants.AsNoTracking()
            .Where(a => a.PublicId == invariantId.Value)
            .Select(a => a.Key)
            .FirstOrDefaultAsync(ct);
    }
}
