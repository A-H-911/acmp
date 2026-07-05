using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Contracts;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Features.GetMinutesAwaiting;

// Committee-wide minutes-approval queue: every minutes record currently InReview, across all meetings,
// newest first. Feeds the secretary dashboard's "MoMs awaiting approval" count/list (AC-065). Only a head
// version is ever InReview (a superseded version is terminal, ADR-0009), so no per-meeting de-dupe is
// needed. Read-all: any authenticated committee member. Reads only Meetings' own tables (ADR-0001).
public sealed record GetMinutesAwaitingQuery : IRequest<IReadOnlyList<MinutesSummaryDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetMinutesAwaitingHandler : IRequestHandler<GetMinutesAwaitingQuery, IReadOnlyList<MinutesSummaryDto>>
{
    private readonly IMeetingsDbContext _db;

    public GetMinutesAwaitingHandler(IMeetingsDbContext db) => _db = db;

    public async Task<IReadOnlyList<MinutesSummaryDto>> Handle(GetMinutesAwaitingQuery request, CancellationToken ct)
    {
        var rows = await _db.Minutes.AsNoTracking()
            .Where(m => m.Status == MinutesStatus.InReview)
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .ToListAsync(ct);
        return rows.Select(MinutesMapping.ToSummary).ToList();
    }
}
