using System;
using System.Linq;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Domain.Events;
using FluentAssertions;
using Xunit;

namespace Acmp.Domain.Tests.Membership;

public class CommitteeMemberTests
{
    [Fact]
    public void Provision_creates_active_member_normalizes_email_and_raises_event()
    {
        var member = CommitteeMember.Provision("kc-1", "Eng Anas", "Anas@Example.com", CommitteeRole.Secretary, DateTimeOffset.UtcNow);

        member.IsActive.Should().BeTrue();
        member.Status.Should().Be(MembershipStatus.Active);
        member.Email.Should().Be("anas@example.com");
        member.Role.Should().Be(CommitteeRole.Secretary);
        member.DomainEvents.Should().ContainSingle(e => e is CommitteeMemberProvisionedEvent);
    }

    [Fact]
    public void Provision_seeds_voting_eligibility_only_for_chairman_and_member()
    {
        CommitteeMember.Provision("kc-c", "Chair", "c@x.com", CommitteeRole.Chairman, DateTimeOffset.UtcNow).IsVotingEligible.Should().BeTrue();
        CommitteeMember.Provision("kc-m", "Mem", "m@x.com", CommitteeRole.Member, DateTimeOffset.UtcNow).IsVotingEligible.Should().BeTrue();
        CommitteeMember.Provision("kc-r", "Rev", "r@x.com", CommitteeRole.Reviewer, DateTimeOffset.UtcNow).IsVotingEligible.Should().BeFalse();
    }

    [Fact]
    public void SyncFromClaims_refreshes_role_and_name_but_keeps_managed_attributes()
    {
        var member = CommitteeMember.Provision("kc-2", "Old Name", "old@x.com", CommitteeRole.Member, DateTimeOffset.UtcNow);
        member.AssignStreams(new long[] { 5, 7 });
        member.SetVotingEligibility(false);

        member.SyncFromClaims("New Name", "new@X.com", CommitteeRole.Reviewer);

        member.FullName.Should().Be("New Name");
        member.Email.Should().Be("new@x.com");
        member.Role.Should().Be(CommitteeRole.Reviewer);
        member.IsVotingEligible.Should().BeFalse();           // managed attribute untouched
        member.Streams.Select(s => s.StreamId).Should().BeEquivalentTo(new long[] { 5, 7 });
    }

    [Fact]
    public void Deactivate_keeps_the_record_for_historical_attribution()
    {
        var member = CommitteeMember.Provision("kc-3", "Reviewer One", "rev@example.com", CommitteeRole.Reviewer, DateTimeOffset.UtcNow);

        member.Deactivate();

        member.IsActive.Should().BeFalse();
        member.Status.Should().Be(MembershipStatus.Disabled);
        member.FullName.Should().Be("Reviewer One");          // attribution intact (AC-058)
        member.Email.Should().Be("rev@example.com");
        member.Role.Should().Be(CommitteeRole.Reviewer);
    }

    [Fact]
    public void AssignStreams_deduplicates_and_replaces()
    {
        var member = CommitteeMember.Provision("kc-4", "Mem", "mem@x.com", CommitteeRole.Member, DateTimeOffset.UtcNow);

        member.AssignStreams(new long[] { 1, 1, 2 });
        member.Streams.Select(s => s.StreamId).Should().BeEquivalentTo(new long[] { 1, 2 });

        member.AssignStreams(new long[] { 3 });
        member.Streams.Select(s => s.StreamId).Should().BeEquivalentTo(new long[] { 3 });
    }
}
