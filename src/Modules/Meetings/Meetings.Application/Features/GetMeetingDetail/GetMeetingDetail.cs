using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Contracts;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Features.GetMeetingDetail;

// Meeting detail by display key (MTG-YYYY-###): the meeting, its agenda + items, attendance roster, and
// captured discussion notes. Powers both the agenda builder (agenda portion) and the live workspace.
// The Prepared-topics pool the builder draws from comes from the Topics API, not here (ADR-0001).
public sealed record GetMeetingDetailQuery(string Key) : IRequest<MeetingDetailDto?>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetMeetingDetailHandler : IRequestHandler<GetMeetingDetailQuery, MeetingDetailDto?>
{
    private readonly IMeetingsDbContext _db;

    public GetMeetingDetailHandler(IMeetingsDbContext db) => _db = db;

    public async Task<MeetingDetailDto?> Handle(GetMeetingDetailQuery request, CancellationToken ct)
    {
        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Key == request.Key, ct);
        if (meeting is null) return null;

        var agenda = await _db.Agendas.AsNoTracking().FirstOrDefaultAsync(a => a.MeetingId == meeting.PublicId, ct);

        return new MeetingDetailDto(
            meeting.PublicId, meeting.Key, meeting.Title, meeting.CommitteeId,
            meeting.ScheduledStart, meeting.ScheduledEnd, meeting.Status.ToString(),
            meeting.Location, meeting.JoinUrl, meeting.ChairUserId, meeting.ChairName,
            meeting.StartedAt, meeting.HeldAt,
            agenda is null ? null : MeetingMapping.ToDto(agenda),
            meeting.Attendees.OrderBy(a => a.Name).Select(MeetingMapping.ToDto).ToList(),
            meeting.Discussions.Select(MeetingMapping.ToDto).ToList());
    }
}
