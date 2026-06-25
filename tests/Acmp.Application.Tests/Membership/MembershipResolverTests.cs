using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Infrastructure.Authorization;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization.Abac;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using LocalizedString = Acmp.Shared.Domain.ValueObjects.LocalizedString;
using Stream = Acmp.Modules.Membership.Domain.Stream;

namespace Acmp.Application.Tests.Membership;

// The real (DbContext-backed) ABAC resolvers Membership exposes to the shared authorization layer.
public class MembershipResolverTests
{
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly DateTimeOffset _now = new(2026, 6, 25, 12, 0, 0, TimeSpan.Zero);

    public MembershipResolverTests() => _clock.UtcNow.Returns(_now);

    private MembershipDbContext NewDb()
    {
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns("seed");
        return new MembershipDbContext(
            new DbContextOptionsBuilder<MembershipDbContext>().UseInMemoryDatabase("res-" + Guid.NewGuid()).Options,
            _clock, user);
    }

    private static CommitteeMember Member(string sub) =>
        CommitteeMember.Provision(sub, sub, sub + "@x.com", Acmp.Modules.Membership.Domain.Enums.CommitteeRole.Member, DateTimeOffset.UtcNow);

    [Fact]
    public async Task TopicCapabilityResolver_returns_active_grants_and_excludes_expired()
    {
        await using var db = NewDb();
        var member = Member("kc-u");
        db.Members.Add(member);
        await db.SaveChangesAsync();

        var topic = Guid.NewGuid();
        db.TopicCapabilities.Add(TopicCapabilityGrant.Grant(member.Id, topic, TopicCapabilityType.Owner));
        db.TopicCapabilities.Add(TopicCapabilityGrant.Grant(member.Id, topic, TopicCapabilityType.Presenter,
            from: _now.AddDays(-2), to: _now.AddDays(-1))); // expired
        await db.SaveChangesAsync();

        var caps = await new TopicCapabilityResolver(db, _clock).GetCapabilitiesAsync("kc-u", topic);

        caps.Should().BeEquivalentTo(new[] { TopicCapabilityType.Owner });
    }

    [Fact]
    public async Task DelegationResolver_reports_only_in_window_grants()
    {
        await using var db = NewDb();
        var delegator = Member("kc-chair");
        var target = Member("kc-deputy");
        db.Members.AddRange(delegator, target);
        await db.SaveChangesAsync();

        db.Delegations.Add(Delegation.Create(delegator.Id, target.Id, "Agenda.Publish", _now.AddDays(-1), _now.AddDays(1)));
        db.Delegations.Add(Delegation.Create(delegator.Id, target.Id, "Vote.Manage", _now.AddDays(-5), _now.AddDays(-1)));
        await db.SaveChangesAsync();

        var resolver = new DelegationResolver(db, _clock);
        (await resolver.HasActiveDelegationAsync("kc-deputy", "Agenda.Publish")).Should().BeTrue();
        (await resolver.HasActiveDelegationAsync("kc-deputy", "Vote.Manage")).Should().BeFalse();   // expired
        (await resolver.HasActiveDelegationAsync("kc-other", "Agenda.Publish")).Should().BeFalse(); // not the delegate
    }

    [Fact]
    public async Task UserStreamProvider_returns_assigned_stream_codes()
    {
        await using var db = NewDb();
        var architecture = Stream.Create("architecture", LocalizedString.Create("Architecture", "الهندسة"));
        var platform = Stream.Create("platform", LocalizedString.Create("Platform", "المنصة"));
        db.Streams.AddRange(architecture, platform);
        var member = Member("kc-u");
        db.Members.Add(member);
        await db.SaveChangesAsync();

        member.AssignStreams(new[] { architecture.Id });
        await db.SaveChangesAsync();

        var codes = await new UserStreamProvider(db).GetAssignedStreamsAsync("kc-u");

        codes.Should().BeEquivalentTo(new[] { "architecture" });
    }
}
