using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Shared.Contracts.Notifications;

// Channel-agnostic notification payload (ADR-0005). v1 channel = in-app notification center only
// (no email, no Webex). Bilingual title/body so the recipient sees it in their locale.
public sealed record NotificationMessage(
    string RecipientUserId,
    LocalizedString Title,
    LocalizedString Body,
    string Category,
    string? DeepLink = null);
