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
        var architecture = Stream.Create("architecture", LocalizedString.Create("Architecture", "الهيكلة"));
        var platform = Stream.Create("platform", LocalizedString.Create("Platform", "المنصة"));
        db.Streams.AddRange(architecture, platform);
        var member = Member("kc-u");
        db.Members.Add(member);
        await db.SaveChangesAsync();

        member.AssignStreams(new[] { architecture.Id });
        await db.SaveChangesAsync();

        var assigned = await new UserStreamProvider(db).GetAssignedStreamsAsync("kc-u");

        assigned.Codes.Should().BeEquivalentTo(new[] { "architecture" });
        assigned.IsUnrestricted.Should().BeFalse("an ordinary delivery stream must not read as the wildcard");
    }

    // DW-026 / ADR-0043 clause (3). The step-5 backfill assigned the WILDCARD to every member who
    // held nothing, so if the provider does not surface that flag those members intersect no topic
    // and are refused everything — the exact opposite of what the backfill exists to prevent.
    //
    // ⚠ THE FLAG COMES FROM THE COLUMN, NOT FROM THE CODE. The wildcard here is created through the
    // same seeded shape production uses and is then read back through the provider; a test that
    // asserted on the string "all-streams" would pass while the control matched a magic value that
    // clause (3) forbids it to match.
    [Fact]
    public async Task UserStreamProvider_reports_a_wildcard_holder_as_unrestricted()
    {
        await using var db = NewDb();
        var core = Stream.Create("core", LocalizedString.Create("Core", "الأساسي"));
        db.Streams.Add(core);
        var member = Member("kc-wild");
        db.Members.Add(member);
        await db.SaveChangesAsync();

        // The wildcard is a seeded singleton with no domain factory (the migration sets the column),
        // so the flag is set the only way a test can: on the entity the provider will read back.
        var wildcard = Stream.Create("all-streams", LocalizedString.Create("All streams", "كل المسارات"));
        typeof(Stream).GetProperty(nameof(Stream.IsWildcard))!.SetValue(wildcard, true);
        db.Streams.Add(wildcard);
        await db.SaveChangesAsync();

        member.AssignStreams(new[] { wildcard.Id });
        await db.SaveChangesAsync();

        var assigned = await new UserStreamProvider(db).GetAssignedStreamsAsync("kc-wild");

        assigned.IsUnrestricted.Should().BeTrue("a member holding the wildcard stream is unrestricted");
        assigned.Codes.Should().NotContain("core", "the flag must not be inferred from holding any stream at all");
    }
}
