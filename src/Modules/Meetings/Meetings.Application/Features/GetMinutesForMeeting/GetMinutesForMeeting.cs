using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Contracts;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Features.GetMinutesForMeeting;

// The version history of a meeting's minutes (newest version first) — the current record is the head. Keyed
// by Meeting.PublicId (a Guid). Readable by any authenticated committee member (read-all).
public sealed record GetMinutesForMeetingQuery(Guid MeetingId) : IRequest<IReadOnlyList<MinutesSummaryDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetMinutesForMeetingHandler : IRequestHandler<GetMinutesForMeetingQuery, IReadOnlyList<MinutesSummaryDto>>
{
    private readonly IMeetingsDbContext _db;

    public GetMinutesForMeetingHandler(IMeetingsDbContext db) => _db = db;

    public async Task<IReadOnlyList<MinutesSummaryDto>> Handle(GetMinutesForMeetingQuery request, CancellationToken ct)
    {
        var rows = await _db.Minutes.AsNoTracking()
            .Where(m => m.MeetingId == request.MeetingId)
            .OrderByDescending(m => m.Version)
            .ToListAsync(ct);
        return rows.Select(MinutesMapping.ToSummary).ToList();
    }
}
