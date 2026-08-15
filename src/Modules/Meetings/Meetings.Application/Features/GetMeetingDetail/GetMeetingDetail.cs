using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Contracts;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Exceptions;
using Acmp.Shared.Contracts.Membership;
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
    private readonly ICommitteeDirectory _directory;
    private readonly ICurrentUser _currentUser;

    public GetMeetingDetailHandler(IMeetingsDbContext db, ICommitteeDirectory directory, ICurrentUser currentUser)
    {
        _db = db;
        _directory = directory;
        _currentUser = currentUser;
    }

    public async Task<MeetingDetailDto?> Handle(GetMeetingDetailQuery request, CancellationToken ct)
    {
        // DEF-073 / AC-011 — null for a committee member, the caller's own meetings for a guest.
        var visible = await GuestPresenterScope.MeetingIdsAsync(_db, _directory, _currentUser, ct);

        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Key == request.Key, ct);

        // 403 AND NOT 404, for the reason GuestSurfaceMiddleware already argues about the paths it
        // blocks: the guest IS authenticated and the meeting does exist, so "not found" would send the
        // SPA down a missing-resource path for what is an authorization answer — and AC-011 says 403.
        //
        // ⚠ A MISSING KEY TAKES THE SAME ANSWER, DELIBERATELY. Answering 404 for an unknown key and 403
        // for a known one turns this route into an existence oracle for a guest, who can no longer see
        // the list that would have told them. One answer for both leaves nothing to probe.
        if (visible is not null && (meeting is null || !visible.Contains(meeting.PublicId)))
            throw new ForbiddenAccessException("This meeting is outside your guest session's scope.");

        if (meeting is null) return null;

        var agenda = await _db.Agendas.AsNoTracking().FirstOrDefaultAsync(a => a.MeetingId == meeting.PublicId, ct);

        return new MeetingDetailDto(
            meeting.PublicId, meeting.Key, meeting.Title, meeting.CommitteeId,
            meeting.ScheduledStart, meeting.ScheduledEnd, meeting.Status.ToString(),
            meeting.Type.ToString(), meeting.Mode.ToString(),
            meeting.Location, meeting.JoinUrl, meeting.ChairUserId, meeting.ChairName,
            meeting.StartedAt, meeting.HeldAt,
            agenda is null ? null : MeetingMapping.ToDto(agenda),
            meeting.Attendees.OrderBy(a => a.Name).Select(MeetingMapping.ToDto).ToList(),
            meeting.Discussions.Select(MeetingMapping.ToDto).ToList(),
            BuildRecording(meeting));
    }

    // At most one recording per meeting: a locally-uploaded file (object key present) or a Webex reference
    // (playback URL present). Uploaded playback is fetched via GET /recording/url; Webex exposes the URL inline.
    private static RecordingDto? BuildRecording(Domain.Meeting m)
    {
        if (m.RecordingObjectKey is not null)
            return new RecordingDto("Uploaded", m.RecordingFileName, m.RecordingContentType,
                m.RecordingSizeBytes, m.RecordingDurationSeconds, null);
        if (m.RecordingUrl is not null)
            return new RecordingDto("Webex", null, null, null, m.RecordingDurationSeconds, m.RecordingUrl);
        return null;
    }
}
