using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Contracts;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Features.GetMeetings;

// Meetings list/calendar feed — newest first. Readable by any authenticated committee member.
public sealed record GetMeetingsQuery : IRequest<IReadOnlyList<MeetingSummaryDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetMeetingsHandler : IRequestHandler<GetMeetingsQuery, IReadOnlyList<MeetingSummaryDto>>
{
    private readonly IMeetingsDbContext _db;

    public GetMeetingsHandler(IMeetingsDbContext db) => _db = db;

    public async Task<IReadOnlyList<MeetingSummaryDto>> Handle(GetMeetingsQuery request, CancellationToken ct)
    {
        var meetings = await _db.Meetings.AsNoTracking()
            .OrderByDescending(m => m.ScheduledStart)
            .ToListAsync(ct);

        var agendas = await _db.Agendas.AsNoTracking().ToListAsync(ct);
        var byMeeting = agendas.ToDictionary(a => a.MeetingId);

        return meetings
            .Select(m => MeetingMapping.ToSummary(m, byMeeting.GetValueOrDefault(m.PublicId)))
            .ToList();
    }
}
