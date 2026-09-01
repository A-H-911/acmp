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

// A meeting's recording: either a locally-uploaded file (Source="Uploaded"; playback via GET /recording/url)
// or a Webex reference (Source="Webex"; PlaybackUrl is the external URL). Null on the meeting DTO when none.
public sealed record RecordingDto(
    string Source,
    string? FileName,
    string? ContentType,
    long? SizeBytes,
    int? DurationSeconds,
    string? PlaybackUrl);

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
    IReadOnlyList<DiscussionDto> Discussions,
    RecordingDto? Recording);

// WBS-26.5 / DW-086 — THE CALENDAR PROJECTION. Deliberately the narrowest shape that answers "which topics
// sit on which day": the meeting's identity, its date, and its agenda's topic ids/keys/titles. Nothing else.
//
// ⚠ WHY THIS EXISTS RATHER THAN REUSING MeetingDetailDto. /meetings carries no topic ids and /meetings/{key}
// carries far too much — attendance, discussions and the recording — so rendering a month from it would fan
// one heavy detail request per meeting. That is DEF-104's N+1 shape, and DW-086 forbids it by name.
//
// ⚠ TopicKey and TopicTitle GO OUT EMPTY FOR A RESTRICTED TOPIC, exactly as AgendaItemDto does. A server-side
// English word would break the EN+AR guardrail; the SPA maps empty to its own localized placeholder.
public sealed record AgendaProjectionItemDto(
    Guid TopicId,
    string TopicKey,
    string TopicTitle);

public sealed record MeetingAgendaProjectionDto(
    Guid MeetingId,
    string MeetingKey,
    DateTimeOffset ScheduledStart,
    IReadOnlyList<AgendaProjectionItemDto> Items);
