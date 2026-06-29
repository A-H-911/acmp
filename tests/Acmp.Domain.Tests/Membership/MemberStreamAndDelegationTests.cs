using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using FluentAssertions;

namespace Acmp.Domain.Tests.Membership;

// MemberStreamAssignment — public constructor + property getter coverage.
// The private parameterless ctor (EF hydration path) is covered in the application-test project
// (MemberStreamAssignmentEfTests) where EF + Infrastructure are available.
public class MemberStreamAssignmentTests
{
    [Fact]
    public void Constructor_stores_StreamId_and_CommitteeMemberId_defaults_to_zero()
    {
        // Arrange + Act
        var assignment = new MemberStreamAssignment(42L);

        // Assert — StreamId is set; CommitteeMemberId is the EF FK, defaults to 0 before persistence
        assignment.StreamId.Should().Be(42L);
        assignment.CommitteeMemberId.Should().Be(0L);
    }

    [Fact]
    public void CommitteeMemberId_is_zero_for_all_assignments_before_EF_saves()
    {
        var member = CommitteeMember.Provision(
            "kc-str", "Stream User", "s@x.com", CommitteeRole.Member, DateTimeOffset.UtcNow);

        member.AssignStreams(new long[] { 10L, 20L });

        // CommitteeMemberId FK is set by EF on SaveChanges; 0 in pure unit context
        member.Streams.Select(s => s.CommitteeMemberId).Should().OnlyContain(id => id == 0L);
        member.Streams.Select(s => s.StreamId).Should().BeEquivalentTo(new long[] { 10L, 20L });
    }
}

// Delegation.IsActiveAt — both branches of `ValidFrom <= now && now <= ValidTo`,
// including boundary values at exactly ValidFrom and ValidTo.
public class DelegationIsActiveAtTests
{
    private static readonly DateTimeOffset From = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 8, 8, 0, 0, 0, TimeSpan.Zero);

    private static Delegation Make() =>
        Delegation.Create(1L, 2L, "Topic.Triage", From, To);

    [Fact]
    public void Returns_false_one_second_before_ValidFrom()
        => Make().IsActiveAt(From.AddSeconds(-1)).Should().BeFalse();

    [Fact]
    public void Returns_true_exactly_at_ValidFrom_boundary()
        => Make().IsActiveAt(From).Should().BeTrue();

    [Fact]
    public void Returns_true_in_the_middle_of_the_window()
        => Make().IsActiveAt(From.AddDays(3)).Should().BeTrue();

    [Fact]
    public void Returns_true_exactly_at_ValidTo_boundary()
        => Make().IsActiveAt(To).Should().BeTrue();

    [Fact]
    public void Returns_false_one_second_after_ValidTo()
        => Make().IsActiveAt(To.AddSeconds(1)).Should().BeFalse();
}

// CommitteeMember — branches not reached by CommitteeMemberTests.cs:
//   • Reactivate() (single-line method, zero existing coverage)
//   • SyncFromClaims on Status == Invited (the Invited → Active flip, line 52–53)
public class CommitteeMemberCoverageTests
{
    [Fact]
    public void Reactivate_restores_active_status_after_deactivation()
    {
        // Arrange
        var member = CommitteeMember.Provision(
            "kc-rx", "Reactivated", "rx@x.com", CommitteeRole.Member, DateTimeOffset.UtcNow);
        member.Deactivate();
        member.IsActive.Should().BeFalse();          // precondition

        // Act
        member.Reactivate();

        // Assert
        member.IsActive.Should().BeTrue();
        member.Status.Should().Be(MembershipStatus.Active);
    }

    [Fact]
    public void SyncFromClaims_flips_Invited_to_Active_on_first_authenticated_login()
    {
        // Arrange — manufacture an Invited record (P4 pre-registration path); Provision() always
        // sets Active and no public Invite() factory exists yet, so reflection reaches the branch.
        var member = CommitteeMember.Provision(
            "kc-inv", "Pre-reg User", "pre@x.com", CommitteeRole.Reviewer, DateTimeOffset.UtcNow);
        typeof(CommitteeMember)
            .GetProperty(nameof(CommitteeMember.Status))!
            .SetValue(member, MembershipStatus.Invited);
        member.Status.Should().Be(MembershipStatus.Invited);   // precondition

        // Act — first login triggers claim sync
        member.SyncFromClaims("Pre-reg User", "pre@x.com", CommitteeRole.Reviewer);

        // Assert — the Invited → Active flip (CommitteeMember.cs lines 52–53)
        member.Status.Should().Be(MembershipStatus.Active);
        member.IsActive.Should().BeTrue();
    }
}
