namespace Acmp.Modules.Meetings.Application.Contracts;

// Read models returned to the SPA. Enums project as their string names (stable wire contract, localized
// in the UI). Chair/presenter/attendee names are display snapshots stored on the aggregate — Meetings
// never joins Membership/Topics tables (ADR-0001).

public sealed record MeetingSummaryDto(
    Guid Id,
    string Key,
    string Title,
    DateTimeOffset ScheduledStart,
    DateTimeOffset ScheduledEnd,
    string Status,
    string Type,
    string Mode,
    string ChairName,
    int ItemCount,
    string AgendaStatus);

public sealed record AgendaItemDto(
    Guid TopicId,
    string TopicKey,
    string TopicTitle,
    bool Urgent,
    int Order,
    int TimeboxMinutes,
    Guid? PresenterUserId,
    string? PresenterName,
    string Outcome,
    int ActualMinutes);

public sealed record AgendaDto(
    Guid Id,
    string Key,
    string Status,
    int Version,
    int TotalTimeboxMinutes,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<AgendaItemDto> Items);

public sealed record AttendanceDto(
    Guid UserId,
    string Name,
    string Role,
    string Status,
    bool IsVotingEligible,
    DateTimeOffset? JoinedAt);

public sealed record DiscussionDto(
    Guid TopicId,
    string Body,
    string AuthorName,
    DateTimeOffset CapturedAt);

public sealed record MeetingDetailDto(
    Guid Id,
    string Key,
    string Title,
    Guid CommitteeId,
    DateTimeOffset ScheduledStart,
    DateTimeOffset ScheduledEnd,
    string Status,
    string Type,
    string Mode,
    string? Location,
    string? JoinUrl,
    Guid ChairUserId,
    string ChairName,
    DateTimeOffset? StartedAt,
    DateTimeOffset? HeldAt,
    AgendaDto? Agenda,
    IReadOnlyList<AttendanceDto> Attendance,
    IReadOnlyList<DiscussionDto> Discussions);
