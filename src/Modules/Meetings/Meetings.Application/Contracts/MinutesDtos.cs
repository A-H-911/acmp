using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Meetings.Application.Contracts;

// Read models returned to the SPA. Enums project as their string names (stable wire contract, localized
// in the UI). Bilingual text is returned as the LocalizedString value object (the SPA picks the locale).
// Minutes reference their Meeting by id + display snapshots (MeetingKey/MeetingTitle) — never a join.

public sealed record MinutesSummaryDto(
    Guid Id,
    string Key,
    int Version,
    Guid MeetingId,
    string MeetingKey,
    string Status,
    DateTimeOffset? PublishedAt);

public sealed record MinutesDetailDto(
    Guid Id,
    string Key,
    int Version,
    Guid MeetingId,
    string MeetingKey,
    string MeetingTitle,
    string Status,
    LocalizedString Summary,
    string? ApprovedByUserId,
    string? ApprovedByName,
    DateTimeOffset? ApprovedAt,
    bool ApprovedBySoleAuthor,
    DateTimeOffset? PublishedAt,
    Guid? SupersededByMinutesId,
    LocalizedString? SupersessionReason);
