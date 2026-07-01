using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Infrastructure.Directory;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Membership;

// The Membership-owned ICommitteeDirectory (the cross-module roster seam). GetActiveMembersInRoleAsync backs
// the headless overdue-escalation sweep, so it must (a) filter by the claims-derived Role cache, (b) exclude
// disabled members, and (c) never throw on an unknown role name.
public class CommitteeDirectoryTests
{
    private static MembershipDbContext NewDb()
    {
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns("seed");
        return new MembershipDbContext(
            new DbContextOptionsBuilder<MembershipDbContext>().UseInMemoryDatabase("dir-" + Guid.NewGuid()).Options,
            Substitute.For<IClock>(), user);
    }

    private static CommitteeMember Member(string sub, CommitteeRole role) =>
        CommitteeMember.Provision(sub, sub, sub + "@acmp.gov", role, DateTimeOffset.UtcNow);

    [Fact]
    public async Task GetActiveMembersInRoleAsync_returns_only_active_members_of_that_role()
    {
        await using var db = NewDb();
        var secretary = Member("kc-sec", CommitteeRole.Secretary);
        var disabledSecretary = Member("kc-sec-off", CommitteeRole.Secretary);
        disabledSecretary.Deactivate();
        db.Members.AddRange(secretary, disabledSecretary,
            Member("kc-chair", CommitteeRole.Chairman), Member("kc-mem", CommitteeRole.Member));
        await db.SaveChangesAsync();

        var secretaries = await new CommitteeDirectory(db).GetActiveMembersInRoleAsync(AcmpRoles.Secretary);

        secretaries.Select(r => r.UserId).Should().BeEquivalentTo(new[] { "kc-sec" });  // not the chairman, member, or disabled
    }

    [Fact]
    public async Task GetActiveMembersInRoleAsync_resolves_the_chairman_tier()
    {
        await using var db = NewDb();
        db.Members.AddRange(Member("kc-chair", CommitteeRole.Chairman), Member("kc-sec", CommitteeRole.Secretary));
        await db.SaveChangesAsync();

        var chairmen = await new CommitteeDirectory(db).GetActiveMembersInRoleAsync(AcmpRoles.Chairman);

        chairmen.Select(r => r.UserId).Should().BeEquivalentTo(new[] { "kc-chair" });
    }

    [Fact]
    public async Task GetActiveMembersInRoleAsync_returns_empty_for_an_unknown_role()
    {
        await using var db = NewDb();
        db.Members.Add(Member("kc-sec", CommitteeRole.Secretary));
        await db.SaveChangesAsync();

        var result = await new CommitteeDirectory(db).GetActiveMembersInRoleAsync("NotARole");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveMembersAsync_lists_every_active_member()
    {
        await using var db = NewDb();
        var disabled = Member("kc-off", CommitteeRole.Member);
        disabled.Deactivate();
        db.Members.AddRange(Member("kc-a", CommitteeRole.Member), Member("kc-b", CommitteeRole.Chairman), disabled);
        await db.SaveChangesAsync();

        var members = await new CommitteeDirectory(db).GetActiveMembersAsync();

        members.Select(r => r.UserId).Should().BeEquivalentTo(new[] { "kc-a", "kc-b" });
    }
}
