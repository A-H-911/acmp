using Acmp.Modules.Notifications.Application.Channels;
using Acmp.Modules.Notifications.Application.Features.GetNotifications;
using Acmp.Modules.Notifications.Application.Features.MarkRead;
using Acmp.Modules.Notifications.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Notifications;

// Round-trips through the real NotificationsDbContext (InMemory): the in-app channel write, the
// current-user-scoped feed query, and the mark-read command (including the IDOR guard — a user can
// never touch another user's notification).
public class NotificationHandlerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private static NotificationsDbContext NewDb(ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<NotificationsDbContext>().UseInMemoryDatabase("notif-" + Guid.NewGuid()).Options, clock, user);

    private static ICurrentUser User(string? sub)
    {
        var u = Substitute.For<ICurrentUser>();
        u.IsAuthenticated.Returns(sub is not null);
        u.UserId.Returns(sub);
        return u;
    }

    private static IClock Clock(DateTimeOffset now)
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(now);
        return c;
    }

    private static NotificationMessage Msg(string recipient, string category = "AgendaPublished", string? link = "/meetings/MTG-2026-001") =>
        new(recipient, LocalizedString.Create("Agenda published", "تم نشر جدول الأعمال"),
            LocalizedString.Create("Body en", "النص"), category, link);

    [Fact]
    public async Task Channel_writes_a_notification_row_for_the_recipient()
    {
        var clock = Clock(T0);
        await using var db = NewDb(User("kc-a"), clock);

        await new InAppNotificationChannel(db).PublishAsync(Msg("kc-a"), default);

        var saved = await db.Notifications.SingleAsync();
        saved.RecipientUserId.Should().Be("kc-a");
        saved.Category.Should().Be("AgendaPublished");
        saved.DeepLink.Should().Be("/meetings/MTG-2026-001");
        saved.IsRead.Should().BeFalse();
        saved.CreatedAt.Should().Be(T0); // stamped by the module DbContext from IClock
    }

    [Fact] // Regression: one NotificationMessage (sharing LocalizedString instances) fanned out to many
           // recipients must not trip EF owned-entity tracking. The agenda/meeting fan-out builds the
           // bilingual content once and reuses it per recipient — pre-fix the 2nd save threw
           // "Notification.Body#LocalizedString.NotificationId is part of a key and cannot be modified".
    public async Task Channel_fans_one_shared_message_out_to_multiple_recipients()
    {
        var clock = Clock(T0);
        await using var db = NewDb(User("kc-a"), clock);
        var channel = new InAppNotificationChannel(db);
        var title = LocalizedString.Create("Agenda published", "تم نشر جدول الأعمال");
        var body = LocalizedString.Create("Body", "النص");

        foreach (var recipient in new[] { "kc-a", "kc-b", "kc-c" })
            await channel.PublishAsync(new NotificationMessage(recipient, title, body, "AgendaPublished", "/x"), default);

        (await db.Notifications.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task GetNotifications_returns_only_the_current_users_items_with_unread_count()
    {
        var clock = Clock(T0);
        await using var db = NewDb(User("kc-a"), clock);
        var channel = new InAppNotificationChannel(db);
        await channel.PublishAsync(Msg("kc-a"), default);
        clock.UtcNow.Returns(T0.AddMinutes(1));
        await channel.PublishAsync(Msg("kc-a"), default);
        await channel.PublishAsync(Msg("kc-b"), default); // another user's item — must not leak

        var feed = await new GetNotificationsHandler(db, User("kc-a")).Handle(new GetNotificationsQuery(), default);

        feed.Items.Should().HaveCount(2);
        feed.UnreadCount.Should().Be(2);
        feed.Items.Should().BeInDescendingOrder(i => i.CreatedAt); // newest first
    }

    [Fact] // Paging (#79 full-page center): page 1 returns the newest PageSize and flags HasMore; the
           // last page clears it. UnreadCount + Total are always the full totals, not just the page.
    public async Task GetNotifications_pages_newest_first_and_reports_total_and_hasMore()
    {
        var clock = Clock(T0);
        await using var db = NewDb(User("kc-a"), clock);
        var channel = new InAppNotificationChannel(db);
        for (var i = 0; i < 3; i++)
        {
            clock.UtcNow.Returns(T0.AddMinutes(i));
            await channel.PublishAsync(Msg("kc-a"), default);
        }

        var page1 = await new GetNotificationsHandler(db, User("kc-a")).Handle(new GetNotificationsQuery(Page: 1, PageSize: 2), default);
        page1.Items.Should().HaveCount(2);
        page1.Total.Should().Be(3);
        page1.UnreadCount.Should().Be(3);
        page1.HasMore.Should().BeTrue();
        page1.Items.Should().BeInDescendingOrder(i => i.CreatedAt);

        var page2 = await new GetNotificationsHandler(db, User("kc-a")).Handle(new GetNotificationsQuery(Page: 2, PageSize: 2), default);
        page2.Items.Should().HaveCount(1);
        page2.HasMore.Should().BeFalse();
    }

    [Fact] // Mark-all flips every unread item of the caller and returns the count; another user's items
           // are untouched (scope = authorization, guardrail 4). A bulk clear emits one audit event.
    public async Task MarkAllRead_flips_only_the_callers_unread_and_returns_count()
    {
        var clock = Clock(T0);
        await using var db = NewDb(User("kc-a"), clock);
        var channel = new InAppNotificationChannel(db);
        await channel.PublishAsync(Msg("kc-a"), default);
        await channel.PublishAsync(Msg("kc-a"), default);
        await channel.PublishAsync(Msg("kc-b"), default);
        var audit = Substitute.For<IAuditSink>();

        var marked = await new MarkAllNotificationsReadHandler(db, User("kc-a"), clock, audit).Handle(new MarkAllNotificationsReadCommand(), default);

        marked.Should().Be(2);
        (await new GetNotificationsHandler(db, User("kc-a")).Handle(new GetNotificationsQuery(), default)).UnreadCount.Should().Be(0);
        (await new GetNotificationsHandler(db, User("kc-b")).Handle(new GetNotificationsQuery(), default)).UnreadCount.Should().Be(1);
        await audit.Received(1).EmitAsync("Notifications.AllRead", "kc-a", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact] // Nothing unread → the count==0 short-circuit returns before persistence, so no audit noise.
    public async Task MarkAllRead_with_nothing_unread_returns_zero_and_emits_no_audit()
    {
        var clock = Clock(T0);
        await using var db = NewDb(User("kc-a"), clock);
        var audit = Substitute.For<IAuditSink>();

        var marked = await new MarkAllNotificationsReadHandler(db, User("kc-a"), clock, audit).Handle(new MarkAllNotificationsReadCommand(), default);

        marked.Should().Be(0);
        await audit.DidNotReceive().EmitAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkRead_flips_the_item_to_read()
    {
        var clock = Clock(T0);
        await using var db = NewDb(User("kc-a"), clock);
        await new InAppNotificationChannel(db).PublishAsync(Msg("kc-a"), default);
        var id = (await db.Notifications.SingleAsync()).PublicId;

        await new MarkNotificationReadHandler(db, User("kc-a"), clock).Handle(new MarkNotificationReadCommand(id), default);

        var feed = await new GetNotificationsHandler(db, User("kc-a")).Handle(new GetNotificationsQuery(), default);
        feed.UnreadCount.Should().Be(0);
        feed.Items.Single().IsRead.Should().BeTrue();
    }

    [Fact] // IDOR guard (guardrail 4): user B cannot mark user A's notification read.
    public async Task MarkRead_rejects_another_users_notification_and_leaves_it_unread()
    {
        var clock = Clock(T0);
        await using var db = NewDb(User("kc-a"), clock);
        await new InAppNotificationChannel(db).PublishAsync(Msg("kc-a"), default);
        var id = (await db.Notifications.SingleAsync()).PublicId;

        var act = () => new MarkNotificationReadHandler(db, User("kc-b"), clock).Handle(new MarkNotificationReadCommand(id), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        (await db.Notifications.SingleAsync()).IsRead.Should().BeFalse();
    }
}
