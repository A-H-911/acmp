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

public sealed record NotificationListDto(IReadOnlyList<NotificationDto> Items, int UnreadCount);
