using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Contracts;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Membership;
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
    private readonly ICommitteeDirectory _directory;
    private readonly ICurrentUser _currentUser;

    public GetMeetingsHandler(IMeetingsDbContext db, ICommitteeDirectory directory, ICurrentUser currentUser)
    {
        _db = db;
        _directory = directory;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MeetingSummaryDto>> Handle(GetMeetingsQuery request, CancellationToken ct)
    {
        // DEF-073 / AC-011 — a guest presenter sees the meetings they present at and no others. Null
        // for every committee member, which is the overwhelmingly common case and costs them nothing.
        var visible = await GuestPresenterScope.MeetingIdsAsync(_db, _directory, _currentUser, ct);

        var meetings = await _db.Meetings.AsNoTracking()
            .OrderByDescending(m => m.ScheduledStart)
            .ToListAsync(ct);

        // FILTERED, NOT REFUSED: a list is a question about a set, so answering it with the caller's
        // own subset is the correct answer rather than a denial. Only NAMING a specific out-of-scope
        // meeting is an access attempt, and GetMeetingDetail answers that one with 403.
        if (visible is not null)
            meetings = meetings.Where(m => visible.Contains(m.PublicId)).ToList();

        var agendas = await _db.Agendas.AsNoTracking().ToListAsync(ct);
        var byMeeting = agendas.ToDictionary(a => a.MeetingId);

        return meetings
            .Select(m => MeetingMapping.ToSummary(m, byMeeting.GetValueOrDefault(m.PublicId)))
            .ToList();
    }
}
