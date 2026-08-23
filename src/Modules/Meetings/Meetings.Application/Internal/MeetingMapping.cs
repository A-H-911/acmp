using Acmp.Modules.Meetings.Application.Contracts;
using Acmp.Modules.Meetings.Domain;
using Acmp.Modules.Meetings.Domain.Enums;

namespace Acmp.Modules.Meetings.Application.Internal;

// Aggregate → read-model projection, shared by the list/detail queries and the command return values.
internal static class MeetingMapping
{
    // FR-163 / C-AUTHZ-04 / AC-114 — THE EGRESS HALF OF THE CONFIDENTIALITY CONTROL.
    //
    // AgendaItem froze TopicKey/TopicTitle/Urgent at agenda-build time (AgendaItem.cs:18-20, ADR-0019), so a
    // Restricted topic placed on an agenda would otherwise hand its title to every member who opens the
    // meeting — the read predicate that guards Topics' own surfaces cannot reach a copy sitting in Meetings'
    // schema. The snapshot is redacted HERE, at projection.
    //
    // ⚠ REDACT AT PROJECTION, NEVER BY MUTATING THE STORED SNAPSHOT (AC-114, INV-005). Published minutes and
    // issued decisions are immutable and AgendaItem freezes its snapshot by design; rewriting those rows
    // would break the immutability the whole audit design rests on.
    //
    // ⚠ THE ITEM IS MASKED, NOT REMOVED. The slot carries meaning of its own — Order, TimeboxMinutes and the
    // "Item 3 of 6" arithmetic every other reader sees — so dropping it would silently give two members
    // different agendas and different totals. Masking withholds the topic while keeping the agenda one shape.
    //
    // ⚠ TopicId IS DELIBERATELY NOT BLANKED, AND THAT IS NOT AN OVERSIGHT. It leaks nothing readable: the only
    // topic read by identifier is GET /api/topics/{key}, which is by KEY and already refuses with 404
    // (GetTopicDetail), and no read-by-guid route exists. Blanking it would break two real things — the SPA
    // keys agenda rows by topicId (MeetingWorkspace.tsx, AgendaBuilder.tsx), so two masked items on one
    // agenda would collide as React keys and break item selection.
    //
    // Key and title go out EMPTY rather than as the word "Restricted": a server-side English string would
    // break the EN+AR guardrail. The SPA maps empty → its own localized placeholder.
    public static AgendaItemDto ToDto(AgendaItem item, bool hidden) => hidden
        ? new(item.TopicId, string.Empty, string.Empty, false, item.Order, item.TimeboxMinutes,
            item.PresenterUserId, item.PresenterName, item.Outcome.ToString(), item.ActualMinutes)
        : new(item.TopicId, item.TopicKey, item.TopicTitle, item.Urgent, item.Order, item.TimeboxMinutes,
            item.PresenterUserId, item.PresenterName, item.Outcome.ToString(), item.ActualMinutes);

    /// <param name="hiddenTopicIds">
    /// From <c>ITopicConfidentiality.GetHiddenTopicIdsAsync</c>, resolved ONCE per meeting — never per item.
    /// </param>
    public static AgendaDto ToDto(Agenda agenda, IReadOnlySet<Guid> hiddenTopicIds) => new(
        agenda.PublicId, agenda.Key, agenda.Status.ToString(), agenda.Version, agenda.TotalTimeboxMinutes,
        agenda.PublishedAt,
        agenda.Items.Select(i => ToDto(i, hiddenTopicIds.Contains(i.TopicId))).ToList());

    /// <summary>The agenda as its EDITORS see it — unredacted, because they may see everything.</summary>
    /// <remarks>
    /// ⚠ THIS OVERLOAD IS SOUND ONLY WHILE ITS CALLERS STAY CHAIRMAN/SECRETARY-GATED. Every one of them is:
    /// the five agenda-builder commands (<c>AgendaBuilderRoles.Editors</c>) and <c>PublishAgenda</c>, all of
    /// which return the agenda to the person who just edited it. Chairman and Secretary are committee-wide
    /// readers under DEC-063 d1, so the hidden set is empty for them by definition and computing it would be
    /// dead weight. IF A CALLER'S <c>AllowedRoles</c> EVER WIDENS, IT MUST MOVE TO THE OVERLOAD ABOVE — this
    /// method name is the grep target that finds them all.
    /// </remarks>
    public static AgendaDto ToDtoForEditor(Agenda agenda) => ToDto(agenda, NothingHidden);

    private static readonly IReadOnlySet<Guid> NothingHidden = new HashSet<Guid>();

    public static AttendanceDto ToDto(Attendance attendee) => new(
        attendee.UserId, attendee.Name, attendee.Role.ToString(), attendee.Status.ToString(),
        attendee.IsVotingEligible, attendee.JoinedAt);

    public static DiscussionDto ToDto(Discussion discussion) => new(
        discussion.TopicId, discussion.Body, discussion.AuthorName, discussion.UpdatedAt ?? discussion.CreatedAt);

    public static MeetingSummaryDto ToSummary(Meeting meeting, Agenda? agenda) => new(
        meeting.PublicId, meeting.Key, meeting.Title, meeting.ScheduledStart, meeting.ScheduledEnd,
        meeting.Status.ToString(), meeting.Type.ToString(), meeting.Mode.ToString(), meeting.ChairName,
        agenda?.Items.Count ?? 0, (agenda?.Status ?? AgendaStatus.Draft).ToString());
}
