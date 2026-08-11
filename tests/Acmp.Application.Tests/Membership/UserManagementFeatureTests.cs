using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Application.Features.AssignRoles;
using Acmp.Modules.Membership.Application.Features.GetMembers;
using Acmp.Modules.Membership.Application.Features.InviteUser;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Membership;

// FR-156..158 / AC-088..093 — in-app user management (ADR-0038).
//
// EVERY GUARD IS PROVEN BY FORCING ITS REFUSAL, not by exercising the happy path and trusting the
// rest. AC-089 says so explicitly, and for a reason this project has paid for repeatedly: four fixed
// defects (DEF-030/031/032, OQ-068) were controls that detected but never told, with the telling
// half asserted in a comment instead of tested. These guards REPLACE the structural guarantee the
// design gave up when it let the app write roles, so a guard that is not proven to refuse is not a
// guard at all.
public class UserManagementFeatureTests
{
    private static MembershipDbContext NewDb(ICurrentUser user)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        return new MembershipDbContext(
            new DbContextOptionsBuilder<MembershipDbContext>().UseInMemoryDatabase("users-" + Guid.NewGuid()).Options,
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

    private static IClock Clock()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        return clock;
    }

    private static CommitteeMember Member(string sub, CommitteeRole role = CommitteeRole.Member) =>
        CommitteeMember.Provision(sub, sub, sub + "@acmp.gov", role, DateTimeOffset.UtcNow);

    // ---- InviteUser (FR-156 / AC-088) ----

    [Fact]
    public async Task Invite_creates_the_account_then_a_member_at_Invited_and_reveals_the_password_once()
    {
        var admin = User("kc-admin", "Administrator");
        await using var db = NewDb(admin);
        var identity = Substitute.For<IIdentityProvider>();
        identity.CreateUserAsync("new@acmp.gov", "New Person", Arg.Any<CancellationToken>())
            .Returns(new InvitedAccount("kc-new", "temp-Pass-123"));
        var audit = Substitute.For<IAuditSink>();

        var result = await new InviteUserHandler(db, identity, Clock(), audit)
            .Handle(new InviteUserCommand("New@ACMP.gov", "New Person"), default);

        result.TemporaryPassword.Should().Be("temp-Pass-123");
        result.Status.Should().Be(nameof(MembershipStatus.Invited));
        result.Email.Should().Be("new@acmp.gov", "the email is normalised before it reaches Keycloak or the row");

        var stored = await db.Members.SingleAsync();
        stored.Status.Should().Be(MembershipStatus.Invited);
        stored.KeycloakUserId.Should().Be("kc-new", "the subject id comes back from the create call, so nothing needs reconciling later");
        await audit.Received(1).EmitEnrichedAsync(
            "Membership.UserInvited", nameof(CommitteeMember), stored.PublicId.ToString(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invite_refuses_a_duplicate_email_WITHOUT_creating_an_identity_account()
    {
        var admin = User("kc-admin", "Administrator");
        await using var db = NewDb(admin);
        db.Members.Add(Member("kc-existing"));
        await db.SaveChangesAsync();
        var identity = Substitute.For<IIdentityProvider>();

        var act = () => new InviteUserHandler(db, identity, Clock(), Substitute.For<IAuditSink>())
            .Handle(new InviteUserCommand("KC-EXISTING@acmp.gov", "Someone Else"), default);

        await act.Should().ThrowAsync<InvalidOperationException>();
        // The point of checking BEFORE calling out: the insert would fail on the unique index anyway,
        // but only after a real account existed in Keycloak for a request that reported failure.
        await identity.DidNotReceive().CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("", "Name")]
    [InlineData("not-an-email", "Name")]
    [InlineData("a@b.com", "")]
    public void Invite_validator_rejects_bad_input(string email, string fullName) =>
        new InviteUserValidator().Validate(new InviteUserCommand(email, fullName)).IsValid.Should().BeFalse();

    [Fact]
    public void Invite_validator_accepts_an_email_and_a_name() =>
        new InviteUserValidator().Validate(new InviteUserCommand("a@b.com", "A B")).IsValid.Should().BeTrue();

    // ---- AssignRoles guards (FR-157 / AC-089) ----

    [Fact]
    public async Task GUARD_1_refuses_changing_your_own_roles()
    {
        var actor = User("kc-self", "Administrator");
        await using var db = NewDb(actor);
        var self = Member("kc-self", CommitteeRole.Administrator);
        db.Members.Add(self);
        await db.SaveChangesAsync();
        var identity = Substitute.For<IIdentityProvider>();

        var act = () => new AssignRolesHandler(db, identity, actor, Substitute.For<IAuditSink>())
            .Handle(new AssignRolesCommand(self.PublicId, new[] { nameof(CommitteeRole.Chairman) }, true), default);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        await identity.DidNotReceive().SetRealmRolesAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GUARD_2_refuses_a_privileged_grant_that_was_not_explicitly_confirmed()
    {
        var actor = User("kc-admin", "Administrator");
        await using var db = NewDb(actor);
        var target = Member("kc-target");
        db.Members.Add(target);
        await db.SaveChangesAsync();
        var identity = Substitute.For<IIdentityProvider>();

        var act = () => new AssignRolesHandler(db, identity, actor, Substitute.For<IAuditSink>())
            .Handle(new AssignRolesCommand(target.PublicId, new[] { nameof(CommitteeRole.Administrator) }), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Administrator*confirmed*");
        await identity.DidNotReceive().SetRealmRolesAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GUARD_3_refuses_removing_the_LAST_Administrator()
    {
        var actor = User("kc-secretary", "Secretary");
        await using var db = NewDb(actor);
        var onlyAdmin = Member("kc-only-admin", CommitteeRole.Administrator);
        db.Members.Add(onlyAdmin);
        await db.SaveChangesAsync();
        var identity = Substitute.For<IIdentityProvider>();

        var act = () => new AssignRolesHandler(db, identity, actor, Substitute.For<IAuditSink>())
            .Handle(new AssignRolesCommand(onlyAdmin.PublicId, new[] { nameof(CommitteeRole.Member) }), default);

        // Without this, one edit locks everyone out of user management permanently and recovery
        // means the Keycloak console by hand — the thing this feature exists to avoid.
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no Administrator*");
        await identity.DidNotReceive().SetRealmRolesAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GUARD_3_allows_demoting_an_Administrator_while_another_one_remains()
    {
        var actor = User("kc-admin-a", "Administrator");
        await using var db = NewDb(actor);
        var demoted = Member("kc-admin-b", CommitteeRole.Administrator);
        db.Members.AddRange(demoted, Member("kc-admin-c", CommitteeRole.Administrator));
        await db.SaveChangesAsync();
        var identity = Substitute.For<IIdentityProvider>();

        await new AssignRolesHandler(db, identity, actor, Substitute.For<IAuditSink>())
            .Handle(new AssignRolesCommand(demoted.PublicId, new[] { nameof(CommitteeRole.Member) }), default);

        (await db.Members.SingleAsync(m => m.PublicId == demoted.PublicId)).Role.Should().Be(CommitteeRole.Member);
    }

    [Fact]
    public async Task Assign_writes_Keycloak_then_mirrors_the_primary_role_then_forces_sign_out_and_audits()
    {
        var actor = User("kc-admin", "Administrator");
        await using var db = NewDb(actor);
        var target = Member("kc-target");
        db.Members.Add(target);
        await db.SaveChangesAsync();
        var identity = Substitute.For<IIdentityProvider>();
        var audit = Substitute.For<IAuditSink>();

        await new AssignRolesHandler(db, identity, actor, audit).Handle(
            new AssignRolesCommand(target.PublicId, new[] { nameof(CommitteeRole.Reviewer), nameof(CommitteeRole.Chairman) }, true),
            default);

        await identity.Received(1).SetRealmRolesAsync("kc-target",
            Arg.Is<IReadOnlyCollection<string>>(r => r.Count == 2), Arg.Any<CancellationToken>());

        // Lowest enum wins, the same rule the token path uses — so the cache matches what the next
        // login would compute rather than a second interpretation of the same set.
        (await db.Members.SingleAsync(m => m.PublicId == target.PublicId)).Role.Should().Be(CommitteeRole.Chairman);

        // AC-090: revocation must be immediate, not deferred to the 60-minute idle timeout.
        await identity.Received(1).SignOutEverywhereAsync("kc-target", Arg.Any<CancellationToken>());

        // AC-093: the row is the control for the accepted SoD risk, so assert it was written.
        await audit.Received(1).EmitEnrichedAsync(
            "Membership.RolesAssigned", nameof(CommitteeMember), target.PublicId.ToString(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assign_refuses_an_unknown_member()
    {
        var actor = User("kc-admin", "Administrator");
        await using var db = NewDb(actor);

        var act = () => new AssignRolesHandler(db, Substitute.For<IIdentityProvider>(), actor, Substitute.For<IAuditSink>())
            .Handle(new AssignRolesCommand(Guid.NewGuid(), new[] { nameof(CommitteeRole.Member) }), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Theory]
    [InlineData("Chairman", true)]
    [InlineData("NotARole", false)]
    public void Assign_validator_only_accepts_committee_roles(string role, bool valid) =>
        new AssignRolesValidator().Validate(new AssignRolesCommand(Guid.NewGuid(), new[] { role })).IsValid.Should().Be(valid);

    [Fact]
    public void Assign_validator_rejects_an_empty_role_set() =>
        new AssignRolesValidator().Validate(new AssignRolesCommand(Guid.NewGuid(), Array.Empty<string>())).IsValid.Should().BeFalse();

    // ---- Roster (FR-158 / AC-091 / DEF-038) ----

    [Fact]
    public async Task Roster_lists_INVITED_members_by_default_and_still_hides_Disabled()
    {
        var admin = User("kc-admin", "Administrator");
        await using var db = NewDb(admin);
        var active = Member("kc-active");
        var invited = CommitteeMember.PreRegister("kc-invited", "Invited Person", "inv@acmp.gov", CommitteeRole.Guest, DateTimeOffset.UtcNow);
        var disabled = Member("kc-disabled");
        disabled.Deactivate();
        db.Members.AddRange(active, invited, disabled);
        await db.SaveChangesAsync();

        var rows = await new GetMembersHandler(db).Handle(new GetMembersQuery(false), default);

        // Invited is NOT "inactive": it means pre-registered and not yet seen. Hiding it is the
        // defect — the roster showed 1 of 26 real members and read as an almost-empty committee.
        rows.Select(r => r.Email).Should().Contain("inv@acmp.gov");
        rows.Should().HaveCount(2);
        rows.Single(r => r.Email == "inv@acmp.gov").Status.Should().Be(nameof(MembershipStatus.Invited));
        rows.Single(r => r.Email == "inv@acmp.gov").IsActive.Should().BeFalse("invited is listed, but it is not active until first sign-in");
    }

    [Fact]
    public async Task Roster_includes_Disabled_only_when_asked()
    {
        var admin = User("kc-admin", "Administrator");
        await using var db = NewDb(admin);
        var disabled = Member("kc-disabled");
        disabled.Deactivate();
        db.Members.Add(disabled);
        await db.SaveChangesAsync();

        (await new GetMembersHandler(db).Handle(new GetMembersQuery(false), default)).Should().BeEmpty();
        (await new GetMembersHandler(db).Handle(new GetMembersQuery(true), default)).Should().HaveCount(1);
    }

    // ---- The invited -> active transition (AC-091) ----

    [Fact]
    public void First_login_flips_an_Invited_record_to_Active_and_reports_it_as_a_change()
    {
        var invited = CommitteeMember.PreRegister("kc-x", "X Y", "x@acmp.gov", CommitteeRole.Member, DateTimeOffset.UtcNow);

        // Identical name, email and role: the ONLY thing that changed is the status, and the
        // transition still has to be reported so it can be audited.
        var changed = invited.SyncFromClaims("X Y", "x@acmp.gov", CommitteeRole.Member);

        changed.Should().BeTrue();
        invited.Status.Should().Be(MembershipStatus.Active);
    }
}
