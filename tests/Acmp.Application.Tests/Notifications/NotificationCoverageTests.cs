using Acmp.Modules.Notifications.Domain;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;

namespace Acmp.Application.Tests.Notifications;

// Exercises the uncovered branches in Notification.Create and Notification.MarkRead:
//   - guard throw when recipientUserId is blank        (line was uncovered)
//   - guard throw when category is blank               (line was uncovered)
//   - whitespace deepLink is normalised to null        (branch was uncovered)
//   - MarkRead idempotency (already-read short-circuit) (branch was uncovered)
public class NotificationCoverageTests
{
    private static readonly LocalizedString Title = LocalizedString.Create("New item", "عنصر جديد");
    private static readonly LocalizedString Body = LocalizedString.Create("You have a new item", "لديك عنصر جديد");

    // ── Notification.Create — guard throws ───────────────────────────────────

    [Fact]
    public void Create_throws_when_recipientUserId_is_blank()
    {
        // Act
        Action act = () => Notification.Create("   ", Title, Body, "topic", "/topics/1");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("recipientUserId");
    }

    [Fact]
    public void Create_throws_when_category_is_blank()
    {
        // Act
        Action act = () => Notification.Create("kc-user", Title, Body, "   ", "/topics/1");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("category");
    }

    // ── Notification.Create — deepLink normalisation ─────────────────────────

    [Fact]
    public void Create_stores_valid_deepLink_unchanged()
    {
        // Act
        var n = Notification.Create("kc-user", Title, Body, "topic", "/topics/42");

        // Assert
        n.DeepLink.Should().Be("/topics/42");
    }

    [Fact]
    public void Create_normalises_whitespace_only_deepLink_to_null()
    {
        // Act  ← IsNullOrWhiteSpace(deepLink) ? null branch was uncovered
        var n = Notification.Create("kc-user", Title, Body, "topic", "   ");

        // Assert
        n.DeepLink.Should().BeNull();
    }

    [Fact]
    public void Create_null_deepLink_stores_null()
    {
        // Act
        var n = Notification.Create("kc-user", Title, Body, "topic", null);

        // Assert
        n.DeepLink.Should().BeNull();
    }

    // ── Notification.Create — field mapping ──────────────────────────────────

    [Fact]
    public void Create_populates_all_fields_correctly()
    {
        // Act
        var n = Notification.Create("kc-user", Title, Body, "meeting", "/meetings/5");

        // Assert
        n.RecipientUserId.Should().Be("kc-user");
        n.Title.Should().Be(Title);
        n.Body.Should().Be(Body);
        n.Category.Should().Be("meeting");
        n.IsRead.Should().BeFalse();
        n.ReadAt.Should().BeNull();
    }

    // ── Notification.MarkRead ────────────────────────────────────────────────

    [Fact]
    public void MarkRead_sets_IsRead_and_ReadAt_on_first_call()
    {
        // Arrange
        var n = Notification.Create("kc-user", Title, Body, "topic", null);
        var now = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);

        // Act
        n.MarkRead(now);

        // Assert
        n.IsRead.Should().BeTrue();
        n.ReadAt.Should().Be(now);
    }

    [Fact]
    public void MarkRead_is_idempotent_and_preserves_original_ReadAt()
    {
        // Arrange  ← second-call `if (IsRead) return;` branch was uncovered
        var n = Notification.Create("kc-user", Title, Body, "topic", null);
        var first = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);
        var second = first.AddHours(1);

        n.MarkRead(first);

        // Act — calling MarkRead a second time should not change ReadAt
        n.MarkRead(second);

        // Assert
        n.IsRead.Should().BeTrue();
        n.ReadAt.Should().Be(first);
    }
}
