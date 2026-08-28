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

    // THE POINT OF THIS METHOD, and the one behaviour that must never drift: it includes DISABLED
    // members. The other two directory methods exclude them because a disabled member is access-blocked
    // and receives no notifications (AC-058) — but the audit register resolves actors through this, and
    // AC-058 keeps a disabled member's record precisely so historical attribution survives (DEF-029:
    // disable, never delete). An active-only lookup would render a raw Keycloak GUID for exactly the
    // departed-member rows a reviewer most needs to read.
    [Fact]
    public async Task ResolveDisplayNamesAsync_includes_DISABLED_members_so_audit_attribution_survives()
    {
        await using var db = NewDb();
        var active = Member("kc-active", CommitteeRole.Member);
        var departed = Member("kc-departed", CommitteeRole.Member);
        departed.Deactivate();
        db.Members.AddRange(active, departed);
        await db.SaveChangesAsync();

        var names = await new CommitteeDirectory(db)
            .ResolveDisplayNamesAsync(new[] { "kc-active", "kc-departed", "kc-never-existed" });

        names.Should().ContainKey("kc-departed").WhoseValue.Should().Be("kc-departed");
        names.Should().ContainKey("kc-active");
        // A subject with no member row (system/integration actor) is absent, so callers fall back to the id.
        names.Should().NotContainKey("kc-never-existed");
    }

    [Fact]
    public async Task ResolveDisplayNamesAsync_is_empty_for_no_input_rather_than_querying()
    {
        await using var db = NewDb();
        db.Members.Add(Member("kc-x", CommitteeRole.Member));
        await db.SaveChangesAsync();

        (await new CommitteeDirectory(db).ResolveDisplayNamesAsync(Array.Empty<string>())).Should().BeEmpty();
        (await new CommitteeDirectory(db).ResolveDisplayNamesAsync(new[] { "  ", "" })).Should().BeEmpty();
    }

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

    // FR-165 / DEC-086 d1 — resolving a member by PublicId rather than by Keycloak subject.
    //
    // WHY A SECOND LOOKUP EXISTS AT ALL. ResolveMemberAsync answers "who is the CALLER", because a token
    // carries a subject. The presenter preview asks the opposite question: the Chairman names a slot, the
    // slot carries AgendaItem.PresenterUserId, and that is a PublicId — so the only way to reach the
    // targeted person's access window without Meetings reading Membership's tables (ADR-0001) is through
    // this port. Everything else about the preview is composition; this is the one genuinely missing seam,
    // and it was found while sizing WBS-24.8 rather than met as a surprise during it.
    //
    // ⚠ THE INVITED CASE IS THE WHOLE TEST, and it is the mutation that matters: swapping the predicate for
    // an active-only one (Status == MembershipStatus.Active) still passes every other test in this file and
    // breaks exactly the principal the preview exists to render. A guest presenter is Invited until their
    // FIRST login, and the Secretary previews them BEFORE the meeting — so the active-only version would
    // return null for every guest who has not yet signed in, which is precisely the population being
    // previewed, and the page would show "not presenting" for a correctly-invited presenter.
    [Fact]
    public async Task ResolveMemberByPublicIdAsync_finds_an_INVITED_guest_and_carries_their_access_window()
    {
        await using var db = NewDb();
        var expiry = DateTimeOffset.Parse("2099-07-02T10:30:00Z");
        var guest = CommitteeMember.PreRegister("kc-guest", "Guest Presenter", "guest@example.org",
            CommitteeRole.Guest, DateTimeOffset.UtcNow);
        guest.SetAccessWindow(expiry);
        db.Members.AddRange(guest, Member("kc-member", CommitteeRole.Member));
        await db.SaveChangesAsync();

        var found = await new CommitteeDirectory(db).ResolveMemberByPublicIdAsync(guest.PublicId);

        found.Should().NotBeNull();
        found!.PublicId.Should().Be(guest.PublicId);
        // The banner's value, and the same stored column the per-request refusal and the sweep read: a
        // preview that invented its own expiry could disagree with the server the presenter will meet.
        found.AccessExpiresAt.Should().Be(expiry);
    }

    // Fail closed. An unknown target is "no such slot", never an unscoped answer — and the preview handler
    // leans on this null to return its empty state rather than composing a view for nobody.
    [Fact]
    public async Task ResolveMemberByPublicIdAsync_is_null_for_an_unknown_public_id()
    {
        await using var db = NewDb();
        db.Members.Add(Member("kc-member", CommitteeRole.Member));
        await db.SaveChangesAsync();

        var found = await new CommitteeDirectory(db).ResolveMemberByPublicIdAsync(Guid.NewGuid());

        found.Should().BeNull();
    }
}
