using System;
using System.Threading;
using System.Threading.Tasks;
using Acmp.Modules.Membership.Application.Features.GetMembers;
using Acmp.Modules.Membership.Application.Features.InviteMember;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Acmp.Application.Tests.Membership;

public class InviteMemberHandlerTests
{
    private static MembershipDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MembershipDbContext>()
            .UseInMemoryDatabase("members-" + Guid.NewGuid())
            .Options;
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns("tester");
        return new MembershipDbContext(options, clock, user);
    }

    [Fact]
    public async Task Invite_then_GetMembers_returns_the_new_member()
    {
        await using var db = NewDb();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);

        var response = await new InviteMemberHandler(db, clock).Handle(
            new InviteMemberCommand("kc-9", "Tester", "t@example.com", CommitteeRole.Member), CancellationToken.None);

        response.Id.Should().BeGreaterThan(0);

        var members = await new GetMembersHandler(db).Handle(new GetMembersQuery(), CancellationToken.None);
        members.Should().ContainSingle(m => m.Email == "t@example.com" && m.Role == "Member");
    }

    [Fact]
    public async Task Invite_rejects_a_duplicate_email()
    {
        await using var db = NewDb();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        var handler = new InviteMemberHandler(db, clock);

        await handler.Handle(new InviteMemberCommand("kc-a", "First", "dup@example.com", CommitteeRole.Member), CancellationToken.None);

        var act = () => handler.Handle(new InviteMemberCommand("kc-b", "Second", "dup@example.com", CommitteeRole.Member), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
