using Acmp.Modules.Actions.Application.Features.ChangeActionStatus;
using Acmp.Modules.Actions.Application.Features.CreateAction;
using Acmp.Modules.Actions.Application.Features.GetActionByKey;
using Acmp.Modules.Actions.Application.Features.GetActionsRegister;
using Acmp.Modules.Actions.Application.Features.VerifyAction;
using Acmp.Modules.Actions.Domain.Enums;
using Acmp.Modules.Actions.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Behaviors;
using Acmp.Shared.Application.Exceptions;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Actions;

// Round-trips through the real ActionsDbContext (InMemory): EF mapping incl. nullable LocalizedString
// columns; the key generator; the W13/W14 command flow. The SoD-1 verifier guard (AC-012/013) is proven
// here — the audited denial and the positive ActionVerified path — since the audit-the-denial requirement
// lives at the handler.
public class ActionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly LocalizedString Title = LocalizedString.Create("Draft the ADR", "صياغة السجل");
    private static readonly Guid Source = Guid.NewGuid();

    private static ActionsDbContext NewDb(ICurrentUser user, IClock clock) => Db("actions-" + Guid.NewGuid(), user, clock);

    private static ActionsDbContext Db(string name, ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<ActionsDbContext>().UseInMemoryDatabase(name).Options, clock, user);

    private static ICurrentUser User(string sub = "kc-sec", string name = "Sam Secretary")
    {
        var u = Substitute.For<ICurrentUser>();
        u.IsAuthenticated.Returns(true);
        u.UserId.Returns(sub);
        u.DisplayName.Returns(name);
        return u;
    }

    private static IClock Clock(DateTimeOffset now)
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(now);
        return c;
    }

    private sealed class RecordingChannel : INotificationChannel
    {
        public List<NotificationMessage> Sent { get; } = new();
        public Task PublishAsync(NotificationMessage message, CancellationToken ct = default) { Sent.Add(message); return Task.CompletedTask; }
    }

    private static CreateActionCommand CreateCmd(string owner = "kc-owner", DateTimeOffset? due = null) =>
        new(Title, null, ActionPriority.Normal, owner, "Owner", due, ActionSourceType.Decision, Source, "DECN-2026-008", "MTG-2026-018");

    // Drafts an action in a NAMED store and returns its id; a fresh context per step mirrors production
    // (one scoped DbContext per request) and sidesteps EF InMemory owned-type change-tracking quirks.
    private static async Task<(string Name, Guid Id, string Key)> CreatedAsync(string owner = "kc-owner")
    {
        var name = "flow-" + Guid.NewGuid();
        await using var db = Db(name, User(), Clock(Now));
        var summary = await new CreateActionHandler(db, new ActionKeyGenerator(db), User(), Clock(Now),
            Substitute.For<IAuditSink>(), Substitute.For<INotificationChannel>())
            .Handle(CreateCmd(owner), CancellationToken.None);
        return (name, summary.Id, summary.Key);
    }

    [Fact]
    public async Task Create_makes_an_open_action_with_a_key_audits_and_notifies_the_owner()
    {
        var user = User("kc-sec"); var clock = Clock(Now);
        await using var db = NewDb(user, clock);
        var audit = Substitute.For<IAuditSink>();
        var channel = new RecordingChannel();

        var summary = await new CreateActionHandler(db, new ActionKeyGenerator(db), user, clock, audit, channel)
            .Handle(CreateCmd(owner: "kc-owner"), CancellationToken.None);

        summary.Key.Should().Be("ACT-2026-001");
        summary.Status.Should().Be("Open");
        summary.OwnerName.Should().Be("Owner");
        await audit.Received(1).EmitEnrichedAsync("Actions.ActionCreated", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        channel.Sent.Should().ContainSingle()
            .Which.Should().Match<NotificationMessage>(m => m.RecipientUserId == "kc-owner" && m.DeepLink == "/actions/ACT-2026-001");
    }

    [Fact] // no self-noise: assigning an action to yourself does not notify you
    public async Task Create_self_assigned_does_not_notify()
    {
        var user = User("kc-sec"); var clock = Clock(Now);
        await using var db = NewDb(user, clock);
        var channel = new RecordingChannel();

        await new CreateActionHandler(db, new ActionKeyGenerator(db), user, clock, Substitute.For<IAuditSink>(), channel)
            .Handle(CreateCmd(owner: "kc-sec"), CancellationToken.None);

        channel.Sent.Should().BeEmpty();
    }

    [Fact] // AC-013: an independent verifier (≠ owner, ≠ completer) verifies → Verified + ActionVerified audit
    public async Task Verify_by_an_independent_actor_moves_to_verified_and_audits()
    {
        var (name, id, key) = await CreatedAsync(owner: "kc-owner");

        await using (var db = Db(name, User("kc-doer"), Clock(Now)))
            await new StartActionHandler(db, Clock(Now), Substitute.For<IAuditSink>(), User("kc-doer"))
                .Handle(new StartActionCommand(id), default);
        await using (var db = Db(name, User("kc-doer"), Clock(Now)))
            await new CompleteActionHandler(db, Clock(Now), Substitute.For<IAuditSink>(), User("kc-doer"))
                .Handle(new CompleteActionCommand(id, null), default);

        var audit = Substitute.For<IAuditSink>();
        var channel = new RecordingChannel();
        await using (var db = Db(name, User("kc-verifier"), Clock(Now)))
            await new VerifyActionHandler(db, User("kc-verifier"), Clock(Now), audit, channel)
                .Handle(new VerifyActionCommand(id), default);

        await using var read = Db(name, User(), Clock(Now));
        var detail = await new GetActionByKeyHandler(read, Clock(Now)).Handle(new GetActionByKeyQuery(key), default);
        detail!.Status.Should().Be("Verified");
        detail.VerifiedByUserId.Should().Be("kc-verifier");
        await audit.Received(1).EmitEnrichedAsync("Actions.ActionVerified", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        channel.Sent.Should().ContainSingle().Which.RecipientUserId.Should().Be("kc-owner"); // owner told it's verified
    }

    [Fact] // AC-012: the OWNER cannot verify their own action — audited denial, 403, status unchanged
    public async Task Verify_by_the_owner_is_denied_audited_and_leaves_it_completed()
    {
        var (name, id, key) = await CreatedAsync(owner: "kc-owner");
        await using (var db = Db(name, User("kc-doer"), Clock(Now)))
            await new StartActionHandler(db, Clock(Now), Substitute.For<IAuditSink>(), User("kc-doer")).Handle(new StartActionCommand(id), default);
        await using (var db = Db(name, User("kc-doer"), Clock(Now)))
            await new CompleteActionHandler(db, Clock(Now), Substitute.For<IAuditSink>(), User("kc-doer")).Handle(new CompleteActionCommand(id, null), default);

        var audit = Substitute.For<IAuditSink>();
        var owner = User("kc-owner", "Owner");

        await using (var db = Db(name, owner, Clock(Now)))
        {
            var act = () => new VerifyActionHandler(db, owner, Clock(Now), audit, Substitute.For<INotificationChannel>())
                .Handle(new VerifyActionCommand(id), default);
            await act.Should().ThrowAsync<ForbiddenAccessException>();
        }

        await audit.Received(1).EmitEnrichedAsync("Actions.ActionVerifyDenied", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await using var read = Db(name, User(), Clock(Now));
        (await new GetActionByKeyHandler(read, Clock(Now)).Handle(new GetActionByKeyQuery(key), default))!.Status.Should().Be("Completed");
    }

    [Fact] // SoD-1 completer arm: whoever marked it complete cannot verify it either
    public async Task Verify_by_the_completer_is_denied()
    {
        var (name, id, _) = await CreatedAsync(owner: "kc-owner");
        await using (var db = Db(name, User("kc-doer"), Clock(Now)))
            await new StartActionHandler(db, Clock(Now), Substitute.For<IAuditSink>(), User("kc-doer")).Handle(new StartActionCommand(id), default);
        await using (var db = Db(name, User("kc-doer"), Clock(Now)))
            await new CompleteActionHandler(db, Clock(Now), Substitute.For<IAuditSink>(), User("kc-doer")).Handle(new CompleteActionCommand(id, null), default);

        await using var vdb = Db(name, User("kc-doer"), Clock(Now));
        var act = () => new VerifyActionHandler(vdb, User("kc-doer"), Clock(Now), Substitute.For<IAuditSink>(), Substitute.For<INotificationChannel>())
            .Handle(new VerifyActionCommand(id), default);
        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Verify_unknown_action_throws_not_found()
    {
        await using var db = NewDb(User(), Clock(Now));
        var act = () => new VerifyActionHandler(db, User(), Clock(Now), Substitute.For<IAuditSink>(), Substitute.For<INotificationChannel>())
            .Handle(new VerifyActionCommand(Guid.NewGuid()), default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact] // register: status filter + derived overdue filter + newest-due sort
    public async Task Register_filters_by_overdue_and_owner()
    {
        var name = "reg-" + Guid.NewGuid();
        // A fresh context per create (same named store) — mirrors production and avoids the EF InMemory
        // owned-type change-tracking quirk that fires when two aggregates with owned values share a context.
        async Task Add(string owner, DateTimeOffset due)
        {
            await using var db = Db(name, User(), Clock(Now));
            await new CreateActionHandler(db, new ActionKeyGenerator(db), User(), Clock(Now),
                Substitute.For<IAuditSink>(), Substitute.For<INotificationChannel>())
                .Handle(CreateCmd(owner: owner, due: due), default);
        }
        await Add("kc-a", Now.AddDays(-2));
        await Add("kc-a", Now.AddDays(5));
        await Add("kc-b", Now.AddDays(-1));

        await using var read = Db(name, User(), Clock(Now));
        var handler = new GetActionsRegisterHandler(read, Clock(Now));

        var all = await handler.Handle(new GetActionsRegisterQuery(), default);
        all.Total.Should().Be(3);

        var overdue = await handler.Handle(new GetActionsRegisterQuery(OverdueOnly: true), default);
        overdue.Items.Should().OnlyContain(a => a.IsOverdue);
        overdue.Total.Should().Be(2);

        var byOwner = await handler.Handle(new GetActionsRegisterQuery(OwnerUserId: "kc-b"), default);
        byOwner.Total.Should().Be(1);
    }

    [Fact] // exercises the start/progress/block/unblock transition handlers + their audit events
    public async Task Lifecycle_transition_handlers_advance_and_audit()
    {
        var (name, id, key) = await CreatedAsync(owner: "kc-owner");
        var audit = Substitute.For<IAuditSink>();
        var actor = User("kc-doer");

        async Task Step(Func<ActionsDbContext, Task> act)
        {
            await using var db = Db(name, actor, Clock(Now)); // fresh context per step (owned-type quirk)
            await act(db);
        }

        await Step(db => new StartActionHandler(db, Clock(Now), audit, actor).Handle(new StartActionCommand(id), default));
        await Step(db => new UpdateActionProgressHandler(db, Clock(Now), audit, actor).Handle(new UpdateActionProgressCommand(id, 40), default));
        await Step(db => new BlockActionHandler(db, Clock(Now), audit, actor).Handle(new BlockActionCommand(id, LocalizedString.Create("blocked", "محجوب")), default));
        await Step(db => new UnblockActionHandler(db, Clock(Now), audit, actor).Handle(new UnblockActionCommand(id), default));

        await using var read = Db(name, User(), Clock(Now));
        var detail = await new GetActionByKeyHandler(read, Clock(Now)).Handle(new GetActionByKeyQuery(key), default);
        detail!.Status.Should().Be("InProgress");
        detail.ProgressPct.Should().Be(40);
        await audit.Received().EmitEnrichedAsync("Actions.ActionBlocked", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await audit.Received().EmitEnrichedAsync("Actions.ActionUnblocked", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await audit.Received().EmitEnrichedAsync("Actions.ActionProgressUpdated", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_handler_moves_to_cancelled()
    {
        var (name, id, key) = await CreatedAsync();
        await using (var db = Db(name, User("kc-doer"), Clock(Now)))
            await new CancelActionHandler(db, Clock(Now), Substitute.For<IAuditSink>(), User("kc-doer"))
                .Handle(new CancelActionCommand(id, LocalizedString.Create("dropped", "أُسقط")), default);

        await using var read = Db(name, User(), Clock(Now));
        (await new GetActionByKeyHandler(read, Clock(Now)).Handle(new GetActionByKeyQuery(key), default))!.Status.Should().Be("Cancelled");
    }

    [Fact] // exercises every register sort arm + the text/key search branch
    public async Task Register_supports_sort_and_search()
    {
        var name = "sort-" + Guid.NewGuid();
        async Task Add(string owner, DateTimeOffset due)
        {
            await using var db = Db(name, User(), Clock(Now));
            await new CreateActionHandler(db, new ActionKeyGenerator(db), User(), Clock(Now),
                Substitute.For<IAuditSink>(), Substitute.For<INotificationChannel>()).Handle(CreateCmd(owner, due), default);
        }
        await Add("kc-a", Now.AddDays(1));
        await Add("kc-b", Now.AddDays(2));

        await using var read = Db(name, User(), Clock(Now));
        var h = new GetActionsRegisterHandler(read, Clock(Now));
        foreach (var by in new[] { "status", "priority", "progress", "created", "due" })
            (await h.Handle(new GetActionsRegisterQuery(SortBy: by, SortDir: "desc"), default)).Total.Should().Be(2);

        (await h.Handle(new GetActionsRegisterQuery(Search: "Draft the ADR"), default)).Total.Should().Be(2); // shared title
        (await h.Handle(new GetActionsRegisterQuery(Search: "ACT-2026-001"), default)).Total.Should().Be(1);   // key match
    }

    // ── validators ──────────────────────────────────────────────────────────
    [Fact]
    public void Create_validator_requires_both_languages_and_an_owner()
    {
        var v = new CreateActionValidator();
        v.Validate(CreateCmd()).IsValid.Should().BeTrue();
        v.Validate(CreateCmd() with { Title = new LocalizedString("EN only", "") }).IsValid.Should().BeFalse();
        v.Validate(CreateCmd() with { OwnerUserId = "" }).IsValid.Should().BeFalse();
        v.Validate(CreateCmd() with { SourceId = Guid.Empty }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Block_validator_requires_a_bilingual_reason()
    {
        var v = new BlockActionValidator();
        v.Validate(new BlockActionCommand(Guid.NewGuid(), LocalizedString.Create("r", "ر"))).IsValid.Should().BeTrue();
        v.Validate(new BlockActionCommand(Guid.NewGuid(), new LocalizedString("only en", ""))).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Cancel_validator_requires_a_bilingual_reason()
    {
        var v = new CancelActionValidator();
        v.Validate(new CancelActionCommand(Guid.NewGuid(), LocalizedString.Create("r", "ر"))).IsValid.Should().BeTrue();
        v.Validate(new CancelActionCommand(Guid.NewGuid(), new LocalizedString("", "ع"))).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Progress_validator_bounds_0_to_100(int pct, bool valid) =>
        new UpdateActionProgressValidator().Validate(new UpdateActionProgressCommand(Guid.NewGuid(), pct)).IsValid.Should().Be(valid);

    // ── authorization pipeline (guardrail 4) ────────────────────────────────
    [Fact]
    public async Task Pipeline_forbids_a_reviewer_from_creating_an_action()
    {
        var reviewer = Substitute.For<ICurrentUser>();
        reviewer.IsAuthenticated.Returns(true);
        reviewer.UserId.Returns("kc-rev");
        reviewer.IsInRole("Reviewer").Returns(true);
        var behavior = new AuthorizationBehavior<CreateActionCommand, Acmp.Modules.Actions.Application.Contracts.ActionSummaryDto>(
            reviewer, Substitute.For<IAuditSink>());

        var act = () => behavior.Handle(CreateCmd(), () => Task.FromResult<Acmp.Modules.Actions.Application.Contracts.ActionSummaryDto>(null!), default);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }
}
