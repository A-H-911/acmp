using Acmp.Modules.Notifications.Application.Channels;
using Acmp.Modules.Notifications.Application.Contracts;
using Acmp.Modules.Notifications.Application.Features.Preferences;
using Acmp.Modules.Notifications.Domain;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Integration.Tests;

/*
 * FR-133 / AC-160 (DEC-186) — the preference store and the in-app filter on REAL SQL Server.
 *
 * WHY THIS FILE HAS TO EXIST. AC-160's "Verified by" clause names integration tests against SQL Server, and
 * two of its properties are invisible on the InMemory provider the other suites run on: the UNIQUE index on
 * (UserId, Category, Channel), which InMemory does not enforce at all, and the filter's query, which InMemory
 * evaluates in C# rather than translating (DEF-066 / the memory rule "has this ever run against SQL Server?").
 * Each test uses its own user id, so the shared container's rows never cross tests.
 */
[Collection(SqlBackstopCollection.Name)]
public sealed class NotificationPreferenceSqlTests
{
    private readonly SqlBackstopFixture _fx;

    public NotificationPreferenceSqlTests(SqlBackstopFixture fx) => _fx = fx;

    private static string NewUser() => $"kc-{Guid.NewGuid():N}";

    private static NotificationMessage Msg(string recipient, string category) =>
        new(recipient, LocalizedString.Create("Agenda published", "تم نشر جدول الأعمال"),
            LocalizedString.Create("Body", "النص"), category, "/meetings/MTG-2026-001");

    [Fact] // One choice per member, event type and channel: the database refuses a second row.
    public async Task A_second_row_for_the_same_member_type_and_channel_is_refused()
    {
        var user = NewUser();
        await using (var seed = _fx.NewNotificationsSql())
        {
            seed.NotificationPreferences.Add(NotificationPreference.Create(user, NotificationCategories.VoteOpened, NotificationChannels.InApp, false));
            await seed.SaveChangesAsync();
        }

        await using var second = _fx.NewNotificationsSql();
        second.NotificationPreferences.Add(NotificationPreference.Create(user, NotificationCategories.VoteOpened, NotificationChannels.InApp, true));
        var act = async () => await second.SaveChangesAsync();

        // Named, because a MISSING table also surfaces as DbUpdateException and would pass a bare check.
        (await act.Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException!.Message.Should().Contain("IX_notification_preferences_UserId_Category_Channel");
    }

    [Fact] // The filter's query translates and runs on SQL Server: opted out -> no row; others -> a row.
    public async Task The_in_app_filter_withholds_only_the_opted_out_recipient_on_sql_server()
    {
        var omar = NewUser();
        var lena = NewUser();
        await using (var seed = _fx.NewNotificationsSql())
        {
            seed.NotificationPreferences.Add(NotificationPreference.Create(omar, NotificationCategories.AgendaPublished, NotificationChannels.InApp, false));
            await seed.SaveChangesAsync();
        }

        await using (var publish = _fx.NewNotificationsSql())
        {
            var channel = new InAppNotificationChannel(publish);
            await channel.PublishAsync(Msg(omar, NotificationCategories.AgendaPublished));
            await channel.PublishAsync(Msg(lena, NotificationCategories.AgendaPublished));
            await channel.PublishAsync(Msg(omar, NotificationCategories.MeetingScheduled));
        }

        await using var read = _fx.NewNotificationsSql();
        var rows = await read.Notifications.Where(n => n.RecipientUserId == omar || n.RecipientUserId == lena)
            .Select(n => new { n.RecipientUserId, n.Category }).ToListAsync();
        rows.Should().BeEquivalentTo(new[]
        {
            new { RecipientUserId = lena, Category = NotificationCategories.AgendaPublished },
            new { RecipientUserId = omar, Category = NotificationCategories.MeetingScheduled },
        });
    }

    [Fact] // The update handler's upsert keeps one row per type on SQL Server, and the read reflects it.
    public async Task Updating_twice_keeps_one_row_and_reads_back_on_sql_server()
    {
        var user = NewUser();
        var caller = new FixedUser(user);

        await using (var db = _fx.NewNotificationsSql())
            await new UpdateNotificationPreferencesHandler(db, caller).Handle(
                new(new[] { new NotificationPreferenceChange(NotificationCategories.RiskEscalated, false) }), default);

        await using (var db = _fx.NewNotificationsSql())
        {
            var result = await new UpdateNotificationPreferencesHandler(db, caller).Handle(
                new(new[] { new NotificationPreferenceChange(NotificationCategories.RiskEscalated, true),
                            new NotificationPreferenceChange(NotificationCategories.TopicSlaBreach, false) }), default);
            result.Items.Single(i => i.Category == NotificationCategories.RiskEscalated).InApp.Should().BeTrue();
            result.Items.Single(i => i.Category == NotificationCategories.TopicSlaBreach).InApp.Should().BeFalse();
        }

        await using var read = _fx.NewNotificationsSql();
        (await read.NotificationPreferences.CountAsync(p => p.UserId == user)).Should().Be(2);
    }

    private sealed class FixedUser : Acmp.Shared.Application.Abstractions.ICurrentUser
    {
        public FixedUser(string id) => UserId = id;
        public bool IsAuthenticated => true;
        public string? UserId { get; }
        public string? UserName => UserId;
        public string? Email => null;
        public string? DisplayName => null;
        public IReadOnlyCollection<string> Roles => Array.Empty<string>();
        public bool IsInRole(string role) => false;
    }
}
