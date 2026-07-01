using Acmp.Modules.Meetings.Application.Contracts;
using Acmp.Modules.Meetings.Domain;

namespace Acmp.Modules.Meetings.Application.Internal;

// Aggregate → read-model projection, shared by the queries and the command return values.
internal static class MinutesMapping
{
    public static MinutesSummaryDto ToSummary(MinutesOfMeeting m) => new(
        m.PublicId, m.Key, m.Version, m.MeetingId, m.MeetingKey, m.Status.ToString(), m.PublishedAt);

    public static MinutesDetailDto ToDetail(MinutesOfMeeting m) => new(
        m.PublicId, m.Key, m.Version, m.MeetingId, m.MeetingKey, m.MeetingTitle, m.Status.ToString(),
        m.Summary, m.ApprovedByUserId, m.ApprovedByName, m.ApprovedAt, m.ApprovedBySoleAuthor, m.PublishedAt,
        m.SupersededByMinutesId, m.SupersessionReason);
}
