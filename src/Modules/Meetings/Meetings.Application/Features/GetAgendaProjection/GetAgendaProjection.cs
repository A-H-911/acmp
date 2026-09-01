using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Contracts;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Modules.Meetings.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Topics;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Features.GetAgendaProjection;

// WBS-26.5 / DW-086 — WHICH TOPICS SIT ON WHICH DAY, for the backlog calendar's month grid.
//
// The grid previously showed a per-meeting COUNT because /meetings carries no topic ids and only
// /meetings/{key} does. DW-086 names the answer and forbids the alternative in the same breath: a
// lightweight projection on the Meetings side, and NOT a fan-out of the detail endpoint across the
// visible month — that is DEF-104's N+1 shape, and each detail response also carries attendance,
// discussions and the recording, so it is a heavy payload fetched for two string fields.
//
// ⚠ AUTHORIZATION IS MIRRORED FROM GetMeetingsQuery, NOT INVENTED. Empty AllowedRoles, so any
// authenticated committee member reads it — the same audience that already reads the list this
// projection decorates. Widening it here would hand the calendar a capability the list does not have.
public sealed record GetAgendaProjectionQuery(DateTimeOffset From, DateTimeOffset To)
    : IRequest<IReadOnlyList<MeetingAgendaProjectionDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();

    // A CALENDAR ASKS FOR A MONTH. The bound exists so this can never become the unbounded read DEF-104
    // records; 366 days leaves a full year (and a leap year) reachable for any future consumer without
    // letting a caller ask for the whole table.
    //
    // ⛔ IT REFUSES RATHER THAN CLAMPING, AND THAT IS DELIBERATE. Silently narrowing a range the caller
    // asked for is DEF-103's shape — the kanban rendered a 25-row prefix and said nothing — where the
    // caller believes they received what they requested. A refusal is visible; a clamp is not.
    public const int MaxRangeDays = 366;
}

public sealed class GetAgendaProjectionHandler
    : IRequestHandler<GetAgendaProjectionQuery, IReadOnlyList<MeetingAgendaProjectionDto>>
{
    private readonly IMeetingsDbContext _db;
    private readonly ICommitteeDirectory _directory;
    private readonly ICurrentUser _currentUser;
    private readonly ITopicConfidentiality _confidentiality;

    public GetAgendaProjectionHandler(IMeetingsDbContext db, ICommitteeDirectory directory,
        ICurrentUser currentUser, ITopicConfidentiality confidentiality)
    {
        _db = db;
        _directory = directory;
        _currentUser = currentUser;
        _confidentiality = confidentiality;
    }

    public async Task<IReadOnlyList<MeetingAgendaProjectionDto>> Handle(
        GetAgendaProjectionQuery request, CancellationToken ct)
    {
        if (request.To <= request.From)
            throw new ValidationException("The range must end after it starts.");

        if ((request.To - request.From).TotalDays > GetAgendaProjectionQuery.MaxRangeDays)
            throw new ValidationException(
                $"The range may not exceed {GetAgendaProjectionQuery.MaxRangeDays} days.");

        // DEF-073 / AC-011 — a guest presenter sees the meetings they present at and no others; null for
        // every committee member. FILTERED, NOT REFUSED, exactly as GetMeetings argues: a range is a
        // question about a set, so the caller's own subset is the correct answer rather than a denial.
        var visible = await GuestPresenterScope.MeetingIdsAsync(_db, _directory, _currentUser, ct);

        var meetingsQuery = _db.Meetings.AsNoTracking()
            .Where(m => m.ScheduledStart >= request.From && m.ScheduledStart < request.To);

        if (visible is not null)
            meetingsQuery = meetingsQuery.Where(m => visible.Contains(m.PublicId));

        var meetings = await meetingsQuery.OrderBy(m => m.ScheduledStart).ToListAsync(ct);
        if (meetings.Count == 0) return Array.Empty<MeetingAgendaProjectionDto>();

        var meetingIds = meetings.Select(m => m.PublicId).ToList();
        var agendas = await _db.Agendas.AsNoTracking()
            .Where(a => meetingIds.Contains(a.MeetingId))
            .ToListAsync(ct);
        var byMeeting = agendas.ToDictionary(a => a.MeetingId);

        // ⛔⛔ THE REDACTION IS NOT OPTIONAL AND IT IS RESOLVED ONCE FOR THE WHOLE RANGE.
        //
        // AgendaItem froze the topic's key and title at build time, so without this the calendar hands a
        // Restricted topic's key and title to the whole committee — GetMeetingDetail's own comment says
        // exactly that about ONE meeting. This is sharper: a bulk read over a whole month, rendered on
        // load with no selection, for every principal who opens the backlog calendar.
        //
        // ⚠ ONCE FOR THE RANGE — never per meeting and never per item. GetMeetingDetail resolves it once
        // per meeting because it serves one; asking per meeting here would reintroduce the N+1 through
        // the back door of the very projection that exists to avoid it (DEF-104, and DW-086's own
        // prohibition). The answer is about the CALLER, not the data, so one call answers for all of them.
        var hidden = (await _confidentiality.GetHiddenTopicIdsAsync(ct)).ToHashSet();

        return meetings.Select(m => new MeetingAgendaProjectionDto(
            m.PublicId,
            m.Key,
            m.ScheduledStart,
            byMeeting.TryGetValue(m.PublicId, out var agenda)
                ? agenda.Items.OrderBy(i => i.Order).Select(i => ToProjection(i, hidden.Contains(i.TopicId))).ToList()
                : Array.Empty<AgendaProjectionItemDto>()))
            .ToList();
    }

    // Key and title go out EMPTY rather than as the word "Restricted", the same convention
    // MeetingMapping.ToDto uses: a server-side English string would break the EN+AR guardrail, and the
    // SPA maps empty to its own localized placeholder. The topic id is retained deliberately — it carries
    // no confidential content and the grid needs a stable React key.
    private static AgendaProjectionItemDto ToProjection(AgendaItem item, bool hidden) => hidden
        ? new(item.TopicId, string.Empty, string.Empty)
        : new(item.TopicId, item.TopicKey, item.TopicTitle);
}
