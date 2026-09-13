using Acmp.Shared.Domain.Entities;

namespace Acmp.Modules.Notifications.Domain;

// One member's on/off choice for one event type on one delivery channel (FR-133 / AC-160, DEC-186;
// DOC-025 §4.1's shape). NO ROW MEANS ON: default-on is load-bearing, because it is what keeps every
// Approved criterion that asserts delivery true for members who have not opted out (DEC-186 e3). UserId is
// the Keycloak subject, the same value NotificationMessage.RecipientUserId carries.
public sealed class NotificationPreference : AuditableEntity
{
    private NotificationPreference() { }

    public string UserId { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Channel { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }

    public static NotificationPreference Create(string userId, string category, string channel, bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("A user is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("A category is required.", nameof(category));
        if (string.IsNullOrWhiteSpace(channel))
            throw new ArgumentException("A channel is required.", nameof(channel));

        return new NotificationPreference { UserId = userId, Category = category, Channel = channel, IsEnabled = isEnabled };
    }

    public void Set(bool isEnabled) => IsEnabled = isEnabled;
}
