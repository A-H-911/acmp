using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Contracts;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Features.GetMinutesByKey;

// Minutes detail by display key (MIN-YYYY-###). A key spans versions (v1 superseded, v2 published, …); the
// query returns the requested Version, or the LATEST when none is given (the current record). Readable by
// any authenticated committee member (read-all).
public sealed record GetMinutesByKeyQuery(string Key, int? Version = null) : IRequest<MinutesDetailDto?>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetMinutesByKeyHandler : IRequestHandler<GetMinutesByKeyQuery, MinutesDetailDto?>
{
    private readonly IMeetingsDbContext _db;

    public GetMinutesByKeyHandler(IMeetingsDbContext db) => _db = db;

    public async Task<MinutesDetailDto?> Handle(GetMinutesByKeyQuery request, CancellationToken ct)
    {
        var query = _db.Minutes.AsNoTracking().Where(m => m.Key == request.Key);
        var minutes = request.Version is { } v
            ? await query.FirstOrDefaultAsync(m => m.Version == v, ct)
            : await query.OrderByDescending(m => m.Version).FirstOrDefaultAsync(ct);
        return minutes is null ? null : MinutesMapping.ToDetail(minutes);
    }
}
