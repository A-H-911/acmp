using Acmp.Modules.Governance.Application.Abstractions;
using Acmp.Modules.Governance.Application.Contracts;
using Acmp.Modules.Governance.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Governance.Application.Features.GetAdrByKey;

// ADR detail by display key (ADR-YYYY-###): the full MADR body, the considered options, approval attribution,
// and the supersession links resolved to peer ADR keys for the detail banners (in-module lookups over the
// governance schema — no cross-module read). Readable by any authenticated committee member (read-all).
public sealed record GetAdrByKeyQuery(string Key) : IRequest<AdrDetailDto?>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetAdrByKeyHandler : IRequestHandler<GetAdrByKeyQuery, AdrDetailDto?>
{
    private readonly IGovernanceDbContext _db;

    public GetAdrByKeyHandler(IGovernanceDbContext db) => _db = db;

    public async Task<AdrDetailDto?> Handle(GetAdrByKeyQuery request, CancellationToken ct)
    {
        var adr = await _db.Adrs.AsNoTracking().FirstOrDefaultAsync(a => a.Key == request.Key, ct);
        if (adr is null) return null;

        // Resolve the peer ADR keys for the supersedes / superseded-by banners (in-module).
        var supersededByKey = await KeyOf(adr.SupersededByAdrId, ct);
        var supersedesKey = await KeyOf(adr.SupersedesAdrId, ct);
        return AdrMapping.ToDetail(adr, supersededByKey, supersedesKey);
    }

    private async Task<string?> KeyOf(Guid? adrId, CancellationToken ct)
    {
        if (adrId is null) return null;
        return await _db.Adrs.AsNoTracking()
            .Where(a => a.PublicId == adrId.Value)
            .Select(a => a.Key)
            .FirstOrDefaultAsync(ct);
    }
}
