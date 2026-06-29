using Acmp.Modules.Membership.Application.Features.CreateDelegation;
using Acmp.Modules.Membership.Application.Features.DeactivateMember;
using Acmp.Modules.Membership.Application.Features.ProvisionCurrentUser;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Membership;

// JIT provisioning (AC-002 claim->role at the handler) and deactivation keeping attribution (AC-058).
public class MembershipFeatureTests
{
    private static MembershipDbContext NewDb(ICurrentUser user)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        return new MembershipDbContext(
            new DbContextOptionsBuilder<MembershipDbContext>().UseInMemoryDatabase("feat-" + Guid.NewGuid()).Options,
            clock, user);
    }

    private static ICurrentUser CurrentUser(string sub, string? name, string? email, params string[] roles)
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns(sub);
        user.DisplayName.Returns(name);
        user.Email.Returns(email);
        user.Roles.Returns(roles);
        return user;
    }

    private static IClock Clock()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        return clock;
    }

    private static IAuditSink Audit() => Substitute.For<IAuditSink>();

    [Fact]
    public async Task Provision_seeds_local_profile_with_role_from_claims()
    {
        var user = CurrentUser("kc-1", "Khalid A.", "khalid@acmp.gov", "Secretary");
        await using var db = NewDb(user);

        var profile = await new ProvisionCurrentUserHandler(db, user, Clock(), Audit())
            .Handle(new ProvisionCurrentUserCommand(), CancellationToken.None);

        profile.Role.Should().Be("Secretary");                 // AC-002
        (await db.Members.SingleAsync()).KeycloakUserId.Should().Be("kc-1");
    }

    [Fact]
    public async Task Provision_picks_the_highest_privilege_role_when_several_are_held()
    {
        var user = CurrentUser("kc-2", "Multi", "m@acmp.gov", "Member", "Chairman");
        await using var db = NewDb(user);

        var profile = await new ProvisionCurrentUserHandler(db, user, Clock(), Audit())
            .Handle(new ProvisionCurrentUserCommand(), CancellationToken.None);

        profile.Role.Should().Be("Chairman");
        profile.Roles.Should().Contain(new[] { "Member", "Chairman" });
    }

    [Fact]
    public async Task Provision_is_idempotent_and_syncs_on_repeat_login()
    {
        var user = CurrentUser("kc-3", "Before", "before@acmp.gov", "Member");
        await using var db = NewDb(user);
        var handler = new ProvisionCurrentUserHandler(db, user, Clock(), Audit());

        await handler.Handle(new ProvisionCurrentUserCommand(), CancellationToken.None);
        user.DisplayName.Returns("After");
        await handler.Handle(new ProvisionCurrentUserCommand(), CancellationToken.None);

        var members = await db.Members.ToListAsync();
        members.Should().ContainSingle();
        members[0].FullName.Should().Be("After");
    }

    [Fact]
    public async Task Provision_denies_an_authenticated_user_with_no_committee_role()
    {
        // AC-003 deny path once past authentication: a validated identity with no recognised role.
        var user = CurrentUser("kc-4", "No Role", "nr@acmp.gov");
        await using var db = NewDb(user);

        var act = () => new ProvisionCurrentUserHandler(db, user, Clock(), Audit())
            .Handle(new ProvisionCurrentUserCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Deactivate_disables_member_but_keeps_attribution()
    {
        var admin = CurrentUser("kc-admin", "Admin", "admin@acmp.gov", "Administrator");
        await using var db = NewDb(admin);
        var member = CommitteeMember.Provision("kc-bob", "Bob", "bob@acmp.gov", CommitteeRole.Member, DateTimeOffset.UtcNow);
        db.Members.Add(member);
        await db.SaveChangesAsync();
        var audit = Audit();

        await new DeactivateMemberHandler(db, admin, audit).Handle(new DeactivateMemberCommand(member.PublicId), CancellationToken.None);

        var stored = await db.Members.SingleAsync(m => m.KeycloakUserId == "kc-bob");
        stored.IsActive.Should().BeFalse();              // AC-058
        stored.FullName.Should().Be("Bob");              // attribution intact
        // State change emits an AuditEvent (docs/26, guardrail 5).
        await audit.Received(1).EmitAsync("Membership.MemberDeactivated", "kc-admin", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deactivate_unknown_member_throws_not_found()
    {
        var admin = CurrentUser("kc-admin", "Admin", "admin@acmp.gov", "Administrator");
        await using var db = NewDb(admin);

        var act = () => new DeactivateMemberHandler(db, admin, Audit()).Handle(new DeactivateMemberCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ---- CreateDelegationValidator (docs/10 §E.3 Auth.Delegate): bounded, identified, capability-named ----

    private static CreateDelegationCommand ValidDelegation() => new(
        Guid.NewGuid(), "Topic.Triage",
        new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 7, 8, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Delegation_is_valid_with_a_target_capability_and_a_forward_window()
    {
        new CreateDelegationValidator().Validate(ValidDelegation()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Delegation_requires_a_target_member()
    {
        new CreateDelegationValidator().Validate(ValidDelegation() with { DelegateMemberPublicId = Guid.Empty })
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Delegation_requires_a_capability_within_length()
    {
        var v = new CreateDelegationValidator();
        v.Validate(ValidDelegation() with { Capability = "" }).IsValid.Should().BeFalse();
        v.Validate(ValidDelegation() with { Capability = new string('x', 129) }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Delegation_window_must_end_after_it_begins()
    {
        var from = new DateTimeOffset(2026, 7, 8, 0, 0, 0, TimeSpan.Zero);
        var v = new CreateDelegationValidator();
        v.Validate(ValidDelegation() with { ValidFrom = from, ValidTo = from }).IsValid.Should().BeFalse();      // equal
        v.Validate(ValidDelegation() with { ValidFrom = from, ValidTo = from.AddDays(-1) }).IsValid.Should().BeFalse(); // inverted
    }
}
