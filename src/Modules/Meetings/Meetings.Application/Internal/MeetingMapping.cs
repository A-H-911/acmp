using Acmp.Modules.Meetings.Application.Contracts;
using Acmp.Modules.Meetings.Domain;
using Acmp.Modules.Meetings.Domain.Enums;

namespace Acmp.Modules.Meetings.Application.Internal;

// Aggregate → read-model projection, shared by the list/detail queries and the command return values.
internal static class MeetingMapping
{
    public static AgendaItemDto ToDto(AgendaItem item) => new(
        item.TopicId, item.TopicKey, item.TopicTitle, item.Urgent, item.Order, item.TimeboxMinutes,
        item.PresenterUserId, item.PresenterName, item.Outcome.ToString(), item.ActualMinutes);

    public static AgendaDto ToDto(Agenda agenda) => new(
        agenda.PublicId, agenda.Key, agenda.Status.ToString(), agenda.Version, agenda.TotalTimeboxMinutes,
        agenda.PublishedAt, agenda.Items.Select(ToDto).ToList());

    public static AttendanceDto ToDto(Attendance attendee) => new(
        attendee.UserId, attendee.Name, attendee.Role.ToString(), attendee.Status.ToString(),
        attendee.IsVotingEligible, attendee.JoinedAt);

    public static DiscussionDto ToDto(Discussion discussion) => new(
        discussion.TopicId, discussion.Body, discussion.AuthorName, discussion.UpdatedAt ?? discussion.CreatedAt);

    public static MeetingSummaryDto ToSummary(Meeting meeting, Agenda? agenda) => new(
        meeting.PublicId, meeting.Key, meeting.Title, meeting.ScheduledStart, meeting.ScheduledEnd,
        meeting.Status.ToString(), meeting.ChairName,
        agenda?.Items.Count ?? 0, (agenda?.Status ?? AgendaStatus.Draft).ToString());
}
