namespace Acmp.Modules.Notifications.Application.Contracts;

// Read DTO for the notification center. Title/body are returned in BOTH languages so the SPA renders
// the recipient's locale (matches the bilingual-DTO convention, e.g. StreamRefDto). CreatedAt is the
// delivery time (stamped by the module DbContext on insert).
public sealed record NotificationDto(
    Guid Id,
    string TitleEn,
    string TitleAr,
    string BodyEn,
    string BodyAr,
    string Category,
    string? DeepLink,
    bool IsRead,
    DateTimeOffset CreatedAt);

// Items = the requested page (newest first). UnreadCount = the user's TOTAL unread across all pages
// (drives the bell badge, not just this page). Total = all of the user's items; HasMore lets the SPA
// page lazily (Load more) without a second count round-trip.
// FR-133 / AC-160: one row per catalog event type, in catalog order. InApp = the member's in-app choice
// (true when they have made none). The Webex column is display-only until WBS-40.19, so it carries no field.
public sealed record NotificationPreferenceDto(string Category, string Group, bool InApp);

public sealed record NotificationPreferencesDto(IReadOnlyList<NotificationPreferenceDto> Items);

public sealed record NotificationPreferenceChange(string Category, bool InApp);

public sealed record NotificationListDto(
    IReadOnlyList<NotificationDto> Items,
    int UnreadCount,
    int Total,
    bool HasMore);
