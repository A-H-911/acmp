using Acmp.Modules.Integrations.Webex;
using Acmp.Modules.Notifications.Application.Channels;
using Acmp.Modules.Notifications.Application.Contracts;
using Acmp.Modules.Notifications.Application.Features.Preferences;
using Acmp.Modules.Notifications.Domain;
using Acmp.Modules.Notifications.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Acmp.Application.Tests.Notifications;

// FR-133 / AC-160 (DEC-186) on the InMemory provider: the in-app sink honours the RECIPIENT's opt-out, no
// stored row means on, the handlers read and write only the caller's own rows, and the shared Webex space
// card is untouched by any member's choice. The unique index and the filter's SQL translation are proven on
// real SQL Server in Acmp.Integration.Tests (NotificationPreferenceSqlTests).
public class NotificationPreferenceTests
{
    private static NotificationsDbContext NewDb(string dbName, string? sub = "kc-a") =>
        new(new DbContextOptionsBuilder<NotificationsDbContext>().UseInMemoryDatabase(dbName).Options, Clock(), User(sub));

    private static ICurrentUser User(string? sub)
    {
        var u = Substitute.For<ICurrentUser>();
        u.IsAuthenticated.Returns(sub is not null);
        u.UserId.Returns(sub);
        return u;
    }

    private static IClock Clock()
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(new DateTimeOffset(2026, 9, 13, 9, 0, 0, TimeSpan.Zero));
        return c;
    }

    private static NotificationMessage Msg(string recipient, string category = NotificationCategories.AgendaPublished) =>
        new(recipient, LocalizedString.Create("Agenda published", "تم نشر جدول الأعمال"),
            LocalizedString.Create("Body", "النص"), category, "/meetings/MTG-2026-001");

    private static async Task OptAsync(NotificationsDbContext db, string user, string category, bool on)
    {
        db.NotificationPreferences.Add(NotificationPreference.Create(user, category, NotificationChannels.InApp, on));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task No_stored_choice_means_the_notification_is_written()
    {
        await using var db = NewDb("pref-" + Guid.NewGuid());
        await new InAppNotificationChannel(db).PublishAsync(Msg("kc-a"));
        (await db.Notifications.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task An_opt_out_withholds_only_that_recipient_and_that_event_type()
    {
        await using var db = NewDb("pref-" + Guid.NewGuid());
        await OptAsync(db, "kc-a", NotificationCategories.AgendaPublished, on: false);
        var channel = new InAppNotificationChannel(db);

        await channel.PublishAsync(Msg("kc-a"));                                          // withheld
        await channel.PublishAsync(Msg("kc-b"));                                          // another member
        await channel.PublishAsync(Msg("kc-a", NotificationCategories.MeetingScheduled)); // another event type

        var rows = await db.Notifications.Select(n => new { n.RecipientUserId, n.Category }).ToListAsync();
        rows.Should().BeEquivalentTo(new[]
        {
            new { RecipientUserId = "kc-b", Category = NotificationCategories.AgendaPublished },
            new { RecipientUserId = "kc-a", Category = NotificationCategories.MeetingScheduled },
        });
    }

    [Fact]
    public async Task A_stored_on_choice_still_delivers()
    {
        await using var db = NewDb("pref-" + Guid.NewGuid());
        await OptAsync(db, "kc-a", NotificationCategories.AgendaPublished, on: true);
        await new InAppNotificationChannel(db).PublishAsync(Msg("kc-a"));
        (await db.Notifications.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task The_filter_ignores_the_signed_in_user_and_reads_the_recipient()
    {
        // A worker job publishes with no signed-in user; a request publishes as someone other than the recipient.
        var name = "pref-" + Guid.NewGuid();
        await using (var seed = NewDb(name, sub: "kc-a")) await OptAsync(seed, "kc-a", NotificationCategories.ActionOverdue, on: false);

        await using var asWorker = NewDb(name, sub: null);
        await new InAppNotificationChannel(asWorker).PublishAsync(Msg("kc-a", NotificationCategories.ActionOverdue));
        await new InAppNotificationChannel(asWorker).PublishAsync(Msg("kc-b", NotificationCategories.ActionOverdue));

        (await asWorker.Notifications.Select(n => n.RecipientUserId).ToListAsync()).Should().Equal("kc-b");
    }

    [Fact]
    public async Task The_shared_webex_space_card_is_unaffected_by_an_opt_out()
    {
        await using var db = NewDb("pref-" + Guid.NewGuid());
        await OptAsync(db, "kc-a", NotificationCategories.AgendaPublished, on: false);
        var scheduler = Substitute.For<IWebexJobScheduler>();
        var webex = new WebexNotificationSink(
            Options.Create(new WebexOptions { Enabled = true, SpaceId = "room", AcmpBaseUrl = "https://acmp.local" }),
            scheduler, NullLogger<WebexNotificationSink>.Instance);

        await new NotificationDispatcher(new INotificationSink[] { new InAppNotificationChannel(db), webex })
            .PublishAsync(Msg("kc-a"));

        (await db.Notifications.CountAsync()).Should().Be(0);
        scheduler.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IWebexJobScheduler.Enqueue)).Should().Be(1);
    }

    [Fact]
    public async Task Get_lists_every_catalog_type_in_order_and_on_by_default()
    {
        await using var db = NewDb("pref-" + Guid.NewGuid());
        var result = await new GetNotificationPreferencesHandler(db, User("kc-a")).Handle(new(), default);

        result.Items.Select(i => (i.Category, i.Group)).Should().Equal(NotificationCategories.All.Select(c => (c.Name, c.Group)));
        result.Items.Should().OnlyContain(i => i.InApp);
    }

    [Fact]
    public async Task Update_stores_the_callers_choice_and_leaves_other_members_alone()
    {
        var name = "pref-" + Guid.NewGuid();
        await using (var db = NewDb(name))
        {
            var result = await new UpdateNotificationPreferencesHandler(db, User("kc-a")).Handle(
                new(new[] { new NotificationPreferenceChange(NotificationCategories.VoteOpened, false) }), default);
            result.Items.Single(i => i.Category == NotificationCategories.VoteOpened).InApp.Should().BeFalse();
            result.Items.Where(i => i.Category != NotificationCategories.VoteOpened).Should().OnlyContain(i => i.InApp);
        }

        await using var read = NewDb(name);
        var other = await new GetNotificationPreferencesHandler(read, User("kc-b")).Handle(new(), default);
        other.Items.Should().OnlyContain(i => i.InApp);
        (await read.NotificationPreferences.SingleAsync()).UserId.Should().Be("kc-a");
    }

    [Fact]
    public async Task Turning_a_type_back_on_updates_the_same_row()
    {
        await using var db = NewDb("pref-" + Guid.NewGuid());
        var handler = new UpdateNotificationPreferencesHandler(db, User("kc-a"));
        await handler.Handle(new(new[] { new NotificationPreferenceChange(NotificationCategories.RiskEscalated, false) }), default);
        var result = await handler.Handle(new(new[] { new NotificationPreferenceChange(NotificationCategories.RiskEscalated, true) }), default);

        result.Items.Should().OnlyContain(i => i.InApp);
        (await db.NotificationPreferences.CountAsync()).Should().Be(1);
        await new InAppNotificationChannel(db).PublishAsync(Msg("kc-a", NotificationCategories.RiskEscalated));
        (await db.Notifications.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Everything_off_means_nothing_reaches_the_centre()
    {
        await using var db = NewDb("pref-" + Guid.NewGuid());
        await new UpdateNotificationPreferencesHandler(db, User("kc-a")).Handle(
            new(NotificationCategories.All.Select(c => new NotificationPreferenceChange(c.Name, false)).ToList()), default);

        var channel = new InAppNotificationChannel(db);
        foreach (var c in NotificationCategories.All) await channel.PublishAsync(Msg("kc-a", c.Name));

        (await db.Notifications.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Both_handlers_refuse_an_anonymous_caller()
    {
        await using var db = NewDb("pref-" + Guid.NewGuid(), sub: null);
        var get = () => new GetNotificationPreferencesHandler(db, User(null)).Handle(new(), default);
        var put = () => new UpdateNotificationPreferencesHandler(db, User(null)).Handle(
            new(new[] { new NotificationPreferenceChange(NotificationCategories.VoteOpened, false) }), default);

        await get.Should().ThrowAsync<UnauthorizedAccessException>();
        await put.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData("MinutesReady")]    // DEF-164's dead name
    [InlineData("agendapublished")] // case matters
    [InlineData("")]
    public void The_validator_refuses_an_event_type_the_catalog_does_not_name(string category)
    {
        var result = new UpdateNotificationPreferencesValidator().Validate(
            new UpdateNotificationPreferencesCommand(new[] { new NotificationPreferenceChange(category, false) }));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void The_validator_refuses_an_empty_list_and_a_repeated_type()
    {
        var validator = new UpdateNotificationPreferencesValidator();
        validator.Validate(new UpdateNotificationPreferencesCommand(Array.Empty<NotificationPreferenceChange>())).IsValid.Should().BeFalse();
        validator.Validate(new UpdateNotificationPreferencesCommand(new[]
        {
            new NotificationPreferenceChange(NotificationCategories.VoteOpened, false),
            new NotificationPreferenceChange(NotificationCategories.VoteOpened, true),
        })).IsValid.Should().BeFalse();
        validator.Validate(new UpdateNotificationPreferencesCommand(new[]
        {
            new NotificationPreferenceChange(NotificationCategories.VoteOpened, false),
        })).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(" ", "c", "InApp", "userId")]
    [InlineData("u", " ", "InApp", "category")]
    [InlineData("u", "c", " ", "channel")]
    public void A_preference_needs_a_user_a_category_and_a_channel(string user, string category, string channel, string param)
    {
        var act = () => NotificationPreference.Create(user, category, channel, false);
        act.Should().Throw<ArgumentException>().WithParameterName(param);
    }
}
