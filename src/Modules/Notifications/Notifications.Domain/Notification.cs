using Acmp.Shared.Domain.Entities;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Notifications.Domain;

// One in-app notification center item for a single recipient (ADR-0005; v1 = in-app only, AC-053).
// Bilingual title/body so the recipient reads it in their locale (guardrail 9). Identity for external
// reference is PublicId — there is no human-readable display key (these are per-user inbox items, not
// governance records). RecipientUserId is the Keycloak subject (ICurrentUser.UserId).
public sealed class Notification : AuditableEntity
{
    private Notification() { }

    public string RecipientUserId { get; private set; } = string.Empty;
    public LocalizedString Title { get; private set; } = null!;
    public LocalizedString Body { get; private set; } = null!;
    public string Category { get; private set; } = string.Empty;
    public string? DeepLink { get; private set; }
    public bool IsRead { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    public static Notification Create(string recipientUserId, LocalizedString title, LocalizedString body,
        string category, string? deepLink)
    {
        if (string.IsNullOrWhiteSpace(recipientUserId))
            throw new ArgumentException("A recipient is required.", nameof(recipientUserId));
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("A category is required.", nameof(category));

        return new Notification
        {
            RecipientUserId = recipientUserId,
            Title = title,
            Body = body,
            Category = category,
            DeepLink = string.IsNullOrWhiteSpace(deepLink) ? null : deepLink,
        };
    }

    // Idempotent: re-marking an already-read item keeps the original ReadAt.
    public void MarkRead(DateTimeOffset now)
    {
        if (IsRead) return;
        IsRead = true;
        ReadAt = now;
    }
}
