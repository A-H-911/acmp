namespace Acmp.Modules.Topics.Application.Contracts;

// Read models returned to the SPA. Enums are projected as their string names (stable wire contract,
// localized in the UI). Owner/actor names are display snapshots stored on the aggregate. AgeDays is
// time since creation; SlaBreached is time-in-current-status beyond the urgency threshold (AC-057).

public sealed record TopicSummaryDto(
    Guid Id,
    string Key,
    string Title,
    string Type,
    string Status,
    string Urgency,
    string Scope,
    IReadOnlyList<string> Streams,
    Guid? OwnerId,
    string? OwnerName,
    int Priority,
    int TimesDeferred,
    int AgeDays,
    bool SlaBreached,
    DateTimeOffset CreatedAt);

public sealed record TopicHistoryDto(string From, string To, string? Reason, string ActorName, DateTimeOffset OccurredAt);

public sealed record TopicCommentDto(Guid Id, string Body, string AuthorName, DateTimeOffset PostedAt);

public sealed record TopicAttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes, string UploadedByName, DateTimeOffset UploadedAt);

public sealed record TopicDetailDto(
    Guid Id,
    string Key,
    string Title,
    string Description,
    string Justification,
    string Type,
    string Status,
    string Urgency,
    string Scope,
    string Source,
    IReadOnlyList<string> Streams,
    IReadOnlyList<string> Systems,
    IReadOnlyList<string> Tags,
    Guid? OwnerId,
    string? OwnerName,
    string SubmittedByName,
    int Priority,
    int AgeDays,
    bool SlaBreached,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevisitOn,
    IReadOnlyList<TopicHistoryDto> History,
    IReadOnlyList<TopicCommentDto> Comments,
    IReadOnlyList<TopicAttachmentDto> Attachments);
