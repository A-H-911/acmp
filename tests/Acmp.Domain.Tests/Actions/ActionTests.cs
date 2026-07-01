using Acmp.Modules.Actions.Domain;
using Acmp.Modules.Actions.Domain.Enums;
using Acmp.Modules.Actions.Domain.Events;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;

namespace Acmp.Domain.Tests.Actions;

// The ActionItem aggregate state machine (docs/12 §7; W13/W14/W22). Proves the legal transitions, the
// wrong-state guards, the derived overdue overlay, and the write-once verification stamps. SoD-1 (verifier
// ≠ owner/completer) is enforced + audited at the handler, so it is asserted in the application suite.
public class ActionTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly LocalizedString Title = LocalizedString.Create("Draft the ADR", "صياغة السجل");
    private static readonly LocalizedString Reason = LocalizedString.Create("Blocked on vendor", "محجوب على المورّد");
    private static readonly Guid Source = Guid.NewGuid();

    private static ActionItem New(DateTimeOffset? due = null) => ActionItem.Create(
        "ACT-2026-001", Title, null, ActionPriority.Normal, "kc-owner", "Owner", due,
        ActionSourceType.Decision, Source, "DECN-2026-008", "MTG-2026-018", Now);

    private static ActionItem InProgress()
    {
        var a = New();
        a.Start(Now);
        return a;
    }

    private static ActionItem Completed()
    {
        var a = InProgress();
        a.Complete(null, "kc-doer", Now);
        return a;
    }

    [Fact]
    public void Create_starts_open_at_zero_progress_and_raises_the_event()
    {
        var a = New();

        a.Status.Should().Be(ActionStatus.Open);
        a.ProgressPct.Should().Be(0);
        a.Key.Should().Be("ACT-2026-001");
        a.OwnerUserId.Should().Be("kc-owner");
        a.SourceKey.Should().Be("DECN-2026-008");
        a.MeetingKey.Should().Be("MTG-2026-018");
        a.DomainEvents.OfType<ActionCreatedEvent>().Should().ContainSingle();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_requires_an_owner(string? owner) =>
        FluentActions.Invoking(() => ActionItem.Create("ACT-2026-001", Title, null, ActionPriority.Normal,
                owner!, "Owner", null, ActionSourceType.Topic, Source, null, null, Now))
            .Should().Throw<InvalidOperationException>();

    [Fact]
    public void Create_requires_a_source_artifact() =>
        FluentActions.Invoking(() => ActionItem.Create("ACT-2026-001", Title, null, ActionPriority.Normal,
                "kc-owner", "Owner", null, ActionSourceType.Topic, Guid.Empty, null, null, Now))
            .Should().Throw<InvalidOperationException>();

    [Fact]
    public void Start_moves_open_to_in_progress()
    {
        var a = New();
        a.Start(Now);
        a.Status.Should().Be(ActionStatus.InProgress);
    }

    [Fact]
    public void Start_from_a_non_open_state_throws()
    {
        var a = InProgress();
        FluentActions.Invoking(() => a.Start(Now)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Block_then_unblock_round_trips_through_blocked()
    {
        var a = InProgress();
        a.Block(Reason, Now);
        a.Status.Should().Be(ActionStatus.Blocked);
        a.BlockedReason.Should().Be(Reason);

        a.Unblock(Now);
        a.Status.Should().Be(ActionStatus.InProgress);
        a.BlockedReason.Should().Be(Reason); // retained as history
    }

    [Fact]
    public void Block_requires_in_progress_and_a_reason()
    {
        FluentActions.Invoking(() => New().Block(Reason, Now)).Should().Throw<InvalidOperationException>(); // still Open
        FluentActions.Invoking(() => InProgress().Block(null!, Now)).Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void UpdateProgress_rejects_out_of_range(int pct) =>
        FluentActions.Invoking(() => InProgress().UpdateProgress(pct)).Should().Throw<InvalidOperationException>();

    [Fact]
    public void UpdateProgress_sets_the_value_while_live()
    {
        var a = InProgress();
        a.UpdateProgress(60);
        a.ProgressPct.Should().Be(60);
    }

    [Fact]
    public void Complete_sets_progress_100_stamps_the_completer_and_requires_in_progress()
    {
        var a = InProgress();
        var note = LocalizedString.Create("Merged in PR 42", "دُمج في الطلب ٤٢");
        a.Complete(note, "kc-doer", Now);

        a.Status.Should().Be(ActionStatus.Completed);
        a.ProgressPct.Should().Be(100);
        a.CompletedByUserId.Should().Be("kc-doer");
        a.CompletionNote.Should().Be(note);

        FluentActions.Invoking(() => New().Complete(null, "kc-doer", Now)).Should().Throw<InvalidOperationException>(); // Open
    }

    [Fact]
    public void Verify_moves_completed_to_verified_and_stamps_the_verifier()
    {
        var a = Completed();
        a.Verify("kc-verifier", "Verifier", Now);

        a.Status.Should().Be(ActionStatus.Verified);
        a.VerifiedByUserId.Should().Be("kc-verifier");
        a.VerifiedByName.Should().Be("Verifier");
        a.VerifiedAt.Should().Be(Now);
    }

    [Fact]
    public void Verify_requires_completed()
    {
        FluentActions.Invoking(() => InProgress().Verify("kc-v", "V", Now)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => Completed().Verify("", "V", Now)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_from_a_non_terminal_state_records_the_reason()
    {
        var a = InProgress();
        var reason = LocalizedString.Create("No longer needed", "لم يعد مطلوبًا");
        a.Cancel(reason, Now);

        a.Status.Should().Be(ActionStatus.Cancelled);
        a.CancelReason.Should().Be(reason);
    }

    [Fact]
    public void Cancel_from_a_terminal_state_throws()
    {
        var verified = Completed();
        verified.Verify("kc-v", "V", Now);
        FluentActions.Invoking(() => verified.Cancel(Reason, Now)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void IsOverdue_is_derived_from_due_date_and_open_status()
    {
        var overdue = New(due: Now.AddDays(-1));
        overdue.IsOverdue(Now).Should().BeTrue();

        New(due: Now.AddDays(1)).IsOverdue(Now).Should().BeFalse();  // future
        New(due: null).IsOverdue(Now).Should().BeFalse();            // no deadline

        var done = Completed();                                       // past-due but completed → not overdue
        done.IsOverdue(Now.AddYears(1)).Should().BeFalse();
    }
}
