using System;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Domain.Events;
using FluentAssertions;
using Xunit;

namespace Acmp.Domain.Tests.Membership;

public class CommitteeMemberTests
{
    [Fact]
    public void Invite_creates_active_member_normalizes_email_and_raises_event()
    {
        var member = CommitteeMember.Invite("kc-1", "Eng Anas", "Anas@Example.com", CommitteeRole.Secretary, DateTimeOffset.UtcNow);

        member.IsActive.Should().BeTrue();
        member.Email.Should().Be("anas@example.com");
        member.Role.Should().Be(CommitteeRole.Secretary);
        member.DomainEvents.Should().ContainSingle(e => e is CommitteeMemberInvitedEvent);
    }

    [Fact]
    public void Deactivate_keeps_the_record_for_historical_attribution()
    {
        var member = CommitteeMember.Invite("kc-2", "Reviewer One", "rev@example.com", CommitteeRole.Reviewer, DateTimeOffset.UtcNow);

        member.Deactivate();

        member.IsActive.Should().BeFalse();
        member.FullName.Should().Be("Reviewer One");
    }
}
