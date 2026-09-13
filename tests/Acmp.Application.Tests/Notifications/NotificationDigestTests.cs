using Acmp.Modules.Notifications.Application.Channels;
using Acmp.Modules.Notifications.Application.Features.Digest;
using Acmp.Modules.Notifications.Domain;
using Acmp.Modules.Notifications.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Actions;
using Acmp.Shared.Contracts.Decisions;
using Acmp.Shared.Contracts.Meetings;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Notifications;

// FR-134 / AC-161 (DEC-188): the digest handler away from Hangfire. The channel is the REAL in-app sink over an
// InMemory store, so "one per period" is checked against rows actually written and a member's opt-out is
// honoured by the same filter every other event type goes through. The three read contracts are substitutes
// here; their queries are proven on SQL Server in Acmp.Integration.Tests (DigestSourceSqlTests).
public class NotificationDigestTests
{
    // Sunday 2026-03-15 09:00 UTC.
    private static readonly DateTimeOffset Sunday = new(2026, 3, 15, 9, 0, 0, TimeSpan.Zero);

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }

    private sealed class Rig
    {
        public FakeClock Clock { get; } = new();
        public NotificationsDbContext Db { get; }
        public ICommitteeDirectory Directory { get; } = Substitute.For<ICommitteeDirectory>();
        public IActionDigestSource Actions { get; } = Substitute.For<IActionDigestSource>();
        public IBallotDigestSource Ballots { get; } = Substitute.For<IBallotDigestSource>();
        public IMeetingDigestSource Meetings { get; } = Substitute.For<IMeetingDigestSource>();
        public DigestOptions Options { get; set; } = new();

        public Rig(DateTimeOffset now, params string[] members)
        {
            Clock.UtcNow = now;
            var user = Substitute.For<ICurrentUser>();
            Db = new(new DbContextOptionsBuilder<NotificationsDbContext>().UseInMemoryDatabase("digest-" + Guid.NewGuid()).Options, Clock, user);
            Directory.GetActiveMembersAsync(Arg.Any<CancellationToken>())
                .Returns(members.Select(m => new CommitteeRecipient(m, m)).ToArray());
            Actions.CountPendingAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
                .Returns(new PendingActionCount(0, 0));
        }

        public Task<int> RunAsync(DigestPeriod period) =>
            new SendDigestHandler(Db, new NotificationDispatcher(new INotificationSink[] { new InAppNotificationChannel(Db) }), Directory, Actions, Ballots, Meetings, Clock,
                Microsoft.Extensions.Options.Options.Create(Options)).Handle(new SendDigestCommand(period), default);

        public Task<List<Notification>> RowsAsync() => Db.Notifications.AsNoTracking().OrderBy(n => n.Id).ToListAsync();
    }

    [Fact]
    public async Task The_daily_digest_gives_the_three_counts_and_links_to_the_dashboard()
    {
        var rig = new Rig(Sunday, "kc-a");
        rig.Actions.CountPendingAsync("kc-a", Sunday, Arg.Any<CancellationToken>()).Returns(new PendingActionCount(3, 1));
        rig.Ballots.CountAwaitingAsync("kc-a", Arg.Any<CancellationToken>()).Returns(2);
        rig.Meetings.CountStartingAsync(Sunday, Sunday.AddDays(1), Arg.Any<CancellationToken>()).Returns(1);

        (await rig.RunAsync(DigestPeriod.Daily)).Should().Be(1);

        var row = (await rig.RowsAsync()).Single();
        row.RecipientUserId.Should().Be("kc-a");
        row.Category.Should().Be(NotificationCategories.DailyDigest);
        row.DeepLink.Should().Be("/");
        row.Title.En.Should().Be("Your daily digest");
        row.Title.Ar.Should().Be("ملخصك اليومي");
        // Counts only: the text is built from four numbers and fixed words, so no title can appear in it.
        row.Body.En.Should().Be("3 pending action(s), 1 of them overdue; 2 open vote(s) awaiting your ballot; 1 meeting(s) starting in the next 24 hours.");
        row.Body.Ar.Should().Be("3 إجراء/إجراءات معلّقة، منها 1 متأخّرة؛ 2 تصويت/تصويتات مفتوحة بانتظار صوتك؛ 1 اجتماع/اجتماعات تبدأ خلال الساعات الـ24 القادمة.");
    }

    [Fact]
    public async Task The_weekly_digest_counts_meetings_over_the_next_seven_days_under_its_own_type()
    {
        var rig = new Rig(Sunday, "kc-a");
        rig.Meetings.CountStartingAsync(Sunday, Sunday.AddDays(7), Arg.Any<CancellationToken>()).Returns(4);

        await rig.RunAsync(DigestPeriod.Weekly);

        var row = (await rig.RowsAsync()).Single();
        row.Category.Should().Be(NotificationCategories.WeeklyDigest);
        row.Title.En.Should().Be("Your weekly digest");
        row.Body.En.Should().EndWith("4 meeting(s) starting in the next 7 days.");
        row.Body.Ar.Should().EndWith("4 اجتماع/اجتماعات تبدأ خلال الأيام السبعة القادمة.");
    }

    [Fact]
    public async Task A_member_whose_three_counts_are_all_zero_receives_nothing()
    {
        var rig = new Rig(Sunday, "kc-idle", "kc-busy");
        rig.Ballots.CountAwaitingAsync("kc-busy", Arg.Any<CancellationToken>()).Returns(1);

        (await rig.RunAsync(DigestPeriod.Daily)).Should().Be(1);

        (await rig.RowsAsync()).Select(r => r.RecipientUserId).Should().Equal("kc-busy");
    }

    [Fact]
    public async Task A_meeting_in_the_window_reaches_every_member_since_meetings_have_no_invitees()
    {
        var rig = new Rig(Sunday, "kc-a", "kc-b");
        rig.Meetings.CountStartingAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(1);

        await rig.RunAsync(DigestPeriod.Daily);

        (await rig.RowsAsync()).Select(r => r.RecipientUserId).Should().BeEquivalentTo("kc-a", "kc-b");
    }

    [Fact]
    public async Task A_second_run_on_the_same_day_sends_nothing_new_and_the_next_day_sends_again()
    {
        var rig = new Rig(Sunday, "kc-a");
        rig.Ballots.CountAwaitingAsync("kc-a", Arg.Any<CancellationToken>()).Returns(1);

        await rig.RunAsync(DigestPeriod.Daily);
        rig.Clock.UtcNow = Sunday.AddHours(14); // 23:00 the same UTC day
        (await rig.RunAsync(DigestPeriod.Daily)).Should().Be(0);
        (await rig.RowsAsync()).Should().HaveCount(1);

        rig.Clock.UtcNow = Sunday.AddDays(1);
        (await rig.RunAsync(DigestPeriod.Daily)).Should().Be(1);
        (await rig.RowsAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task The_day_boundary_is_read_in_the_configured_time_zone()
    {
        // 20:00 UTC Sunday is 23:00 in Riyadh (UTC+3, no DST); 21:30 UTC is 00:30 Monday there. The same two
        // runs on UTC fall on one day (the test above), so only the configured zone makes the second one send.
        var rig = new Rig(Sunday.AddHours(11), "kc-a") { Options = new DigestOptions { TimeZone = "Asia/Riyadh" } };
        rig.Ballots.CountAwaitingAsync("kc-a", Arg.Any<CancellationToken>()).Returns(1);

        await rig.RunAsync(DigestPeriod.Daily);
        rig.Clock.UtcNow = Sunday.AddHours(12.5);
        (await rig.RunAsync(DigestPeriod.Daily)).Should().Be(1);
        (await rig.RowsAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task The_weekly_period_runs_from_monday_so_a_sunday_rerun_sends_nothing_new()
    {
        var monday = Sunday.AddDays(-6);
        var rig = new Rig(monday, "kc-a");
        rig.Ballots.CountAwaitingAsync("kc-a", Arg.Any<CancellationToken>()).Returns(1);

        await rig.RunAsync(DigestPeriod.Weekly);
        rig.Clock.UtcNow = Sunday; // same week
        (await rig.RunAsync(DigestPeriod.Weekly)).Should().Be(0);
        rig.Clock.UtcNow = Sunday.AddDays(1); // next Monday
        (await rig.RunAsync(DigestPeriod.Weekly)).Should().Be(1);

        (await rig.RowsAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task The_daily_and_weekly_periods_are_independent()
    {
        var rig = new Rig(Sunday, "kc-a");
        rig.Ballots.CountAwaitingAsync("kc-a", Arg.Any<CancellationToken>()).Returns(1);

        await rig.RunAsync(DigestPeriod.Daily);
        (await rig.RunAsync(DigestPeriod.Weekly)).Should().Be(1);

        (await rig.RowsAsync()).Select(r => r.Category)
            .Should().Equal(NotificationCategories.DailyDigest, NotificationCategories.WeeklyDigest);
    }

    [Fact]
    public async Task A_member_who_turned_the_daily_digest_off_gets_no_daily_but_still_the_weekly()
    {
        var rig = new Rig(Sunday, "kc-a", "kc-b");
        rig.Ballots.CountAwaitingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(1);
        rig.Db.NotificationPreferences.Add(NotificationPreference.Create("kc-a", NotificationCategories.DailyDigest, NotificationChannels.InApp, false));
        await rig.Db.SaveChangesAsync();

        await rig.RunAsync(DigestPeriod.Daily);
        await rig.RunAsync(DigestPeriod.Weekly);

        (await rig.RowsAsync()).Select(r => (r.RecipientUserId, r.Category)).Should().BeEquivalentTo(new[]
        {
            ("kc-b", NotificationCategories.DailyDigest),
            ("kc-a", NotificationCategories.WeeklyDigest),
            ("kc-b", NotificationCategories.WeeklyDigest),
        });
    }

    [Fact]
    public void The_time_zone_setting_is_utc_when_unset_and_resolves_a_known_name()
    {
        new DigestOptions().ResolveTimeZone().Should().Be(TimeZoneInfo.Utc);
        new DigestOptions { TimeZone = " " }.ResolveTimeZone().Should().Be(TimeZoneInfo.Utc);
        new DigestOptions { TimeZone = "Asia/Riyadh" }.ResolveTimeZone().GetUtcOffset(Sunday).Should().Be(TimeSpan.FromHours(3));
    }

    [Fact]
    public void An_unknown_time_zone_name_is_refused_with_the_name_in_the_error()
    {
        var act = () => new DigestOptions { TimeZone = "Mars/Olympus_Mons" }.ResolveTimeZone();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Digest:TimeZone*Mars/Olympus_Mons*");
    }

    [Fact]
    public void The_schedules_default_to_doc_025s_times()
    {
        var o = new DigestOptions();
        o.DailyCron.Should().Be("30 7 * * *");
        o.WeeklyCron.Should().Be("0 8 * * 1");
    }
}
