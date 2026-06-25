using Acmp.Modules.Membership.Application.Features.AssignStreams;
using Acmp.Modules.Membership.Application.Features.CreateDelegation;
using Acmp.Modules.Membership.Application.Features.GetStreams;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using LocalizedString = Acmp.Shared.Domain.ValueObjects.LocalizedString;
using Stream = Acmp.Modules.Membership.Domain.Stream;

namespace Acmp.Application.Tests.Membership;

// Direct coverage for the admin membership handlers (AssignStreams, CreateDelegation, GetStreams),
// including the AuditEvent each state change now emits (docs/26, guardrail 5).
public class MembershipAdminFeatureTests
{
    private static MembershipDbContext NewDb(ICurrentUser user)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        return new MembershipDbContext(
            new DbContextOptionsBuilder<MembershipDbContext>().UseInMemoryDatabase("admin-" + Guid.NewGuid()).Options,
            clock, user);
    }

    private static ICurrentUser User(string sub, params string[] roles)
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns(sub);
        user.Roles.Returns(roles);
        return user;
    }

    private static CommitteeMember Member(string sub) =>
        CommitteeMember.Provision(sub, sub, sub + "@acmp.gov", CommitteeRole.Member, DateTimeOffset.UtcNow);

    // ---- AssignStreams ----

    [Fact]
    public async Task AssignStreams_sets_known_streams_ignores_unknown_and_audits()
    {
        var admin = User("kc-admin", "Administrator");
        await using var db = NewDb(admin);
        var architecture = Stream.Create("architecture", LocalizedString.Create("Architecture", "الهندسة"));
        db.Streams.Add(architecture);
        var member = Member("kc-u");
        db.Members.Add(member);
        await db.SaveChangesAsync();
        var audit = Substitute.For<IAuditSink>();

        await new AssignStreamsHandler(db, admin, audit).Handle(
            new AssignStreamsCommand(member.PublicId, new[] { architecture.PublicId, Guid.NewGuid() }), CancellationToken.None);

        var stored = await db.Members.SingleAsync(m => m.KeycloakUserId == "kc-u");
        stored.Streams.Select(s => s.StreamId).Should().BeEquivalentTo(new[] { architecture.Id });
        await audit.Received(1).EmitAsync("Membership.StreamsAssigned", "kc-admin", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignStreams_unknown_member_throws_not_found()
    {
        var admin = User("kc-admin", "Administrator");
        await using var db = NewDb(admin);

        var act = () => new AssignStreamsHandler(db, admin, Substitute.For<IAuditSink>())
            .Handle(new AssignStreamsCommand(Guid.NewGuid(), Array.Empty<Guid>()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ---- CreateDelegation ----

    [Fact]
    public async Task CreateDelegation_persists_and_audits()
    {
        var chair = User("kc-chair", "Chairman");
        await using var db = NewDb(chair);
        var delegator = Member("kc-chair");
        var target = Member("kc-deputy");
        db.Members.AddRange(delegator, target);
        await db.SaveChangesAsync();
        var audit = Substitute.For<IAuditSink>();

        var now = DateTimeOffset.UtcNow;
        var publicId = await new CreateDelegationHandler(db, chair, audit).Handle(
            new CreateDelegationCommand(target.PublicId, "Agenda.Publish", now, now.AddDays(7)), CancellationToken.None);

        publicId.Should().NotBeEmpty();
        (await db.Delegations.SingleAsync()).Capability.Should().Be("Agenda.Publish");
        await audit.Received(1).EmitAsync("Membership.DelegationCreated", "kc-chair", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateDelegation_rejects_unprovisioned_delegator()
    {
        var stranger = User("kc-ghost", "Chairman"); // authorized role but no local member record
        await using var db = NewDb(stranger);
        var target = Member("kc-deputy");
        db.Members.Add(target);
        await db.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        var act = () => new CreateDelegationHandler(db, stranger, Substitute.For<IAuditSink>())
            .Handle(new CreateDelegationCommand(target.PublicId, "Agenda.Publish", now, now.AddDays(1)), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task CreateDelegation_unknown_delegate_throws_not_found()
    {
        var chair = User("kc-chair", "Chairman");
        await using var db = NewDb(chair);
        db.Members.Add(Member("kc-chair"));
        await db.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        var act = () => new CreateDelegationHandler(db, chair, Substitute.For<IAuditSink>())
            .Handle(new CreateDelegationCommand(Guid.NewGuid(), "Agenda.Publish", now, now.AddDays(1)), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ---- GetStreams ----

    [Fact]
    public async Task GetStreams_returns_all_streams_ordered_by_code()
    {
        var user = User("kc-u", "Member");
        await using var db = NewDb(user);
        db.Streams.AddRange(
            Stream.Create("platform", LocalizedString.Create("Platform", "المنصة")),
            Stream.Create("architecture", LocalizedString.Create("Architecture", "الهندسة")));
        await db.SaveChangesAsync();

        var streams = await new GetStreamsHandler(db).Handle(new GetStreamsQuery(), CancellationToken.None);

        streams.Select(s => s.Code).Should().Equal("architecture", "platform");
        streams[0].NameAr.Should().Be("الهندسة");
    }
}
