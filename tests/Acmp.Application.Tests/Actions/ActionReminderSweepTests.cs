using Acmp.Modules.Actions.Application.Reminders;
using Acmp.Modules.Actions.Domain;
using Acmp.Modules.Actions.Domain.Enums;
using Acmp.Modules.Actions.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Acmp.Application.Tests.Actions;

// The W22 / docs/29 §3.4 reminder + escalation sweep, driven against a FAKE clock + in-memory actions (no
// Hangfire). Adversarial / failure-first: boundaries (T-3 exact, day-7 vs day-8), exclusions (done/cancelled/
// no-due-date), idempotency (already-sent, re-run), the configured overdue rhythm, headless robustness
// (no active Secretary), and the save-before-send concurrency guarantee.
public class ActionReminderSweepTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 3, 15, 9, 0, 0, TimeSpan.Zero);
    private static readonly LocalizedString Title = LocalizedString.Create("Draft the ADR", "صياغة السجل");
    private static readonly LocalizedString Reason = LocalizedString.Create("blocked", "معطّل");

    // A due date N calendar days from the sweep's "today" (negative = overdue).
    private static DateTimeOffset Due(int days) => new(Now.UtcDateTime.Date.AddDays(days), TimeSpan.Zero);

    private static IClock Clock(DateTimeOffset now)
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(now);
        return c;
    }

    private static ICurrentUser User()
    {
        var u = Substitute.For<ICurrentUser>();
        u.UserId.Returns("seed");
        return u;
    }

    private static ActionsDbContext Db(string name) =>
        new(new DbContextOptionsBuilder<ActionsDbContext>().UseInMemoryDatabase(name).Options, Clock(Now), User());

    private sealed class RecordingChannel : INotificationChannel
    {
        public List<NotificationMessage> Sent { get; } = new();
        public Task PublishAsync(NotificationMessage message, CancellationToken ct = default) { Sent.Add(message); return Task.CompletedTask; }
    }

    private static ICommitteeDirectory Dir(string[]? secretaries = null, string[]? chairmen = null)
    {
        var d = Substitute.For<ICommitteeDirectory>();
        d.GetActiveMembersInRoleAsync(AcmpRoles.Secretary, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyCollection<CommitteeRecipient>)(secretaries ?? Array.Empty<string>()).Select(s => new CommitteeRecipient(s, s)).ToArray());
        d.GetActiveMembersInRoleAsync(AcmpRoles.Chairman, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyCollection<CommitteeRecipient>)(chairmen ?? Array.Empty<string>()).Select(s => new CommitteeRecipient(s, s)).ToArray());
        return d;
    }

    private static ActionItem Action(string key, DateTimeOffset? due, ActionStatus status = ActionStatus.Open,
        string owner = "kc-owner")
    {
        var a = ActionItem.Create(key, Title, null, ActionPriority.Normal, owner, "Owner", due,
            ActionSourceType.Decision, Guid.NewGuid(), "DECN-2026-001", null, CreatedAt);
        switch (status)
        {
            case ActionStatus.InProgress: a.Start(CreatedAt); break;
            case ActionStatus.Blocked: a.Start(CreatedAt); a.Block(Reason, CreatedAt); break;
            case ActionStatus.Completed: a.Start(CreatedAt); a.Complete(null, "kc-completer", CreatedAt); break;
            case ActionStatus.Cancelled: a.Cancel(Reason, CreatedAt); break;
        }
        return a;
    }

    private static async Task SeedAsync(string db, params ActionItem[] actions)
    {
        await using var ctx = Db(db);
        ctx.Actions.AddRange(actions);
        await ctx.SaveChangesAsync();
    }

    private static async Task<(ActionReminderSweepResult Result, RecordingChannel Channel, IAuditSink Audit)> RunAsync(
        string db, ActionReminderOptions? opts = null, ICommitteeDirectory? dir = null, DateTimeOffset? now = null)
    {
        var channel = new RecordingChannel();
        var audit = Substitute.For<IAuditSink>();
        await using var ctx = Db(db);
        var handler = new SweepActionRemindersHandler(ctx, Clock(now ?? Now), channel, dir ?? Dir(), audit,
            Options.Create(opts ?? new ActionReminderOptions()));
        var result = await handler.Handle(new SweepActionRemindersCommand(), CancellationToken.None);
        return (result, channel, audit);
    }

    // ---- Due reminder --------------------------------------------------------------------------------

    [Fact]
    public async Task Outside_the_window_sends_nothing()
    {
        var db = "sweep-" + Guid.NewGuid();
        await SeedAsync(db, Action("ACT-1", Due(5)));         // due in 5 days, window is 3

        var (result, channel, _) = await RunAsync(db);

        channel.Sent.Should().BeEmpty();
        result.Should().Be(new ActionReminderSweepResult(0, 0, 0));
    }

    [Fact]
    public async Task Exactly_at_the_window_sends_one_due_reminder_to_the_owner()
    {
        var db = "sweep-" + Guid.NewGuid();
        await SeedAsync(db, Action("ACT-1", Due(3)));         // exactly 3 days out

        var (result, channel, audit) = await RunAsync(db);

        channel.Sent.Should().ContainSingle()
            .Which.Should().Match<NotificationMessage>(m =>
                m.RecipientUserId == "kc-owner" && m.Category == "ActionDueReminder" && m.DeepLink == "/actions/ACT-1");
        result.DueReminders.Should().Be(1);
        await audit.Received(1).EmitAsync("Actions.RemindersSent", "system:action-reminders", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task One_day_past_the_window_does_not_remind()
    {
        var db = "sweep-" + Guid.NewGuid();
        await SeedAsync(db, Action("ACT-1", Due(4)));         // just outside the 3-day window

        var (_, channel, _) = await RunAsync(db);

        channel.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Due_today_still_sends_a_due_reminder()
    {
        var db = "sweep-" + Guid.NewGuid();
        await SeedAsync(db, Action("ACT-1", Due(0)));

        var (result, channel, _) = await RunAsync(db);

        channel.Sent.Should().ContainSingle().Which.Category.Should().Be("ActionDueReminder");
        result.DueReminders.Should().Be(1);
    }

    [Fact]
    public async Task Due_reminder_is_one_shot_across_runs()
    {
        var db = "sweep-" + Guid.NewGuid();
        await SeedAsync(db, Action("ACT-1", Due(2)));

        (await RunAsync(db)).Channel.Sent.Should().ContainSingle();  // first run reminds
        (await RunAsync(db)).Channel.Sent.Should().BeEmpty();        // second run: marker set → silent
    }

    // ---- Overdue rhythm ------------------------------------------------------------------------------

    [Fact]
    public async Task Overdue_notifies_the_owner_with_the_day_count()
    {
        var db = "sweep-" + Guid.NewGuid();
        await SeedAsync(db, Action("ACT-1", Due(-2), ActionStatus.InProgress));

        var (result, channel, _) = await RunAsync(db);

        channel.Sent.Should().ContainSingle()
            .Which.Should().Match<NotificationMessage>(m => m.RecipientUserId == "kc-owner" && m.Category == "ActionOverdue");
        result.OverdueNotices.Should().Be(1);
    }

    [Fact]
    public async Task Blocked_overdue_action_is_still_notified()
    {
        var db = "sweep-" + Guid.NewGuid();
        await SeedAsync(db, Action("ACT-1", Due(-1), ActionStatus.Blocked));

        var (_, channel, _) = await RunAsync(db);

        channel.Sent.Should().ContainSingle().Which.Category.Should().Be("ActionOverdue");
    }

    [Fact]
    public async Task Once_mode_notifies_overdue_only_a_single_time()
    {
        var db = "sweep-" + Guid.NewGuid();
        await SeedAsync(db, Action("ACT-1", Due(-2)));
        var opts = new ActionReminderOptions { OverdueMode = OverdueReminderMode.Once };

        (await RunAsync(db, opts)).Channel.Sent.Should().ContainSingle();
        (await RunAsync(db, opts, now: Now.AddDays(1))).Channel.Sent.Should().BeEmpty();  // next day, still Once → silent
    }

    [Fact]
    public async Task DailyWhileOverdue_deduplicates_within_a_day_but_resends_the_next_day()
    {
        var db = "sweep-" + Guid.NewGuid();
        await SeedAsync(db, Action("ACT-1", Due(-2)));  // default OverdueMode = DailyWhileOverdue

        (await RunAsync(db)).Channel.Sent.Should().ContainSingle();                 // first sweep of the day
        (await RunAsync(db, now: Now.AddHours(6))).Channel.Sent.Should().BeEmpty(); // same calendar day → deduped
        (await RunAsync(db, now: Now.AddDays(1))).Channel.Sent.Should().ContainSingle(); // next day → resends
    }

    // ---- Escalation tiers ----------------------------------------------------------------------------

    [Fact]
    public async Task Not_escalated_at_exactly_the_secretary_threshold()
    {
        var db = "sweep-" + Guid.NewGuid();
        await SeedAsync(db, Action("ACT-1", Due(-7)));  // exactly 7 days → NOT > 7
        var dir = Dir(secretaries: new[] { "kc-sec" });

        var (result, channel, _) = await RunAsync(db, dir: dir);

        channel.Sent.Should().OnlyContain(m => m.Category == "ActionOverdue");  // owner notice only
        result.Escalations.Should().Be(0);
    }

    [Fact]
    public async Task Escalates_to_the_secretary_after_the_threshold()
    {
        var db = "sweep-" + Guid.NewGuid();
        await SeedAsync(db, Action("ACT-1", Due(-8)));  // 8 days → > 7
        var dir = Dir(secretaries: new[] { "kc-sec-1", "kc-sec-2" });

        var (result, channel, _) = await RunAsync(db, dir: dir);

        channel.Sent.Should().Contain(m => m.RecipientUserId == "kc-sec-1" && m.Category == "ActionOverdueEscalation");
        channel.Sent.Should().Contain(m => m.RecipientUserId == "kc-sec-2" && m.Category == "ActionOverdueEscalation");
        result.Escalations.Should().Be(2);
    }

    [Fact]
    public async Task Secretary_escalation_is_one_shot()
    {
        var db = "sweep-" + Guid.NewGuid();
        await SeedAsync(db, Action("ACT-1", Due(-8)));
        var dir = Dir(secretaries: new[] { "kc-sec" });

        (await RunAsync(db, dir: dir)).Channel.Sent.Should().Contain(m => m.Category == "ActionOverdueEscalation");
        // Next day: owner notice may resend (DailyWhileOverdue) but the secretary is NOT re-escalated.
        (await RunAsync(db, dir: dir, now: Now.AddDays(1))).Channel.Sent
            .Should().NotContain(m => m.Category == "ActionOverdueEscalation");
    }

    [Fact]
    public async Task Escalates_to_the_chairman_after_fourteen_days()
    {
        var db = "sweep-" + Guid.NewGuid();
        await SeedAsync(db, Action("ACT-1", Due(-15)));  // > 14
        var dir = Dir(secretaries: new[] { "kc-sec" }, chairmen: new[] { "kc-chair" });

        var (result, channel, _) = await RunAsync(db, dir: dir);

        channel.Sent.Should().Contain(m => m.RecipientUserId == "kc-chair" && m.Category == "ActionOverdueEscalation");
        channel.Sent.Should().Contain(m => m.RecipientUserId == "kc-sec" && m.Category == "ActionOverdueEscalation");
        result.Escalations.Should().Be(2);  // one secretary + one chairman
    }

    [Fact]
    public async Task No_active_secretary_does_not_crash_and_sends_no_escalation()
    {
        var db = "sweep-" + Guid.NewGuid();
        await SeedAsync(db, Action("ACT-1", Due(-9)));
        var dir = Dir();  // nobody holds any role

        var (result, channel, _) = await RunAsync(db, dir: dir);

        channel.Sent.Should().OnlyContain(m => m.Category == "ActionOverdue");  // just the owner notice
        result.Escalations.Should().Be(0);
    }

    // ---- Exclusions ----------------------------------------------------------------------------------

    [Theory]
    [InlineData(ActionStatus.Completed)]
    [InlineData(ActionStatus.Cancelled)]
    public async Task Terminal_actions_are_never_swept(ActionStatus status)
    {
        var db = "sweep-" + Guid.NewGuid();
        await SeedAsync(db, Action("ACT-1", Due(-10), status));

        var (result, channel, _) = await RunAsync(db);

        channel.Sent.Should().BeEmpty();
        result.Should().Be(new ActionReminderSweepResult(0, 0, 0));
    }

    [Fact]
    public async Task Actions_without_a_due_date_are_never_swept()
    {
        var db = "sweep-" + Guid.NewGuid();
        await SeedAsync(db, Action("ACT-1", due: default));  // Create allows a null due date

        var (_, channel, _) = await RunAsync(db);

        channel.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Empty_backlog_sends_nothing()
    {
        var db = "sweep-" + Guid.NewGuid();

        var (result, channel, audit) = await RunAsync(db);

        channel.Sent.Should().BeEmpty();
        result.Should().Be(new ActionReminderSweepResult(0, 0, 0));
        await audit.DidNotReceive().EmitAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    // ---- Concurrency guarantee -----------------------------------------------------------------------

    // ActionsDbContext is sealed, so we wrap the interface: query flows to the real InMemory context, but the
    // save throws — exactly the shape of a concurrent RowVersion clash the sweep must not swallow.
    private sealed class ThrowOnSaveDbContext : Acmp.Modules.Actions.Application.Abstractions.IActionsDbContext
    {
        private readonly ActionsDbContext _inner;
        public ThrowOnSaveDbContext(ActionsDbContext inner) => _inner = inner;
        public DbSet<ActionItem> Actions => _inner.Actions;
        public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
            throw new DbUpdateConcurrencyException("simulated concurrent edit");
    }

    [Fact]
    public async Task A_concurrent_edit_aborts_the_sweep_without_sending_anything()
    {
        var db = "sweep-" + Guid.NewGuid();
        await SeedAsync(db, Action("ACT-1", Due(-2)));  // would otherwise send an overdue notice
        var channel = new RecordingChannel();

        await using var inner = Db(db);
        var handler = new SweepActionRemindersHandler(new ThrowOnSaveDbContext(inner), Clock(Now), channel, Dir(),
            Substitute.For<IAuditSink>(), Options.Create(new ActionReminderOptions()));

        // Markers are committed BEFORE any send, so a save failure surfaces (Hangfire retries) and nothing was sent.
        await FluentActions.Awaiting(() => handler.Handle(new SweepActionRemindersCommand(), CancellationToken.None))
            .Should().ThrowAsync<DbUpdateConcurrencyException>();
        channel.Sent.Should().BeEmpty();
    }
}
