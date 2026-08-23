using Acmp.Modules.Membership.Application.Features.SetVotingEligibility;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Membership;

// DEF-041 / DEC-046 d4 — changing who may vote.
//
// CommitteeMember.SetVotingEligibility has existed since P4 with NO CALLER: the directory drew a
// switch the design intends to be operable and the app rendered an inert span. Coverage never
// catches that shape — it catches unread state, never an uncalled method (the same gap that left
// PUT /api/topics/{id} without an SPA caller until ca3bf05).
public class SetVotingEligibilityTests
{
    [Fact]
    public async Task Setting_eligibility_stores_the_requested_state_and_audits_it()
    {
        await using var db = NewDb();
        var member = CommitteeMember.Provision("kc-1", "Voter One", "v1@acmp.gov", CommitteeRole.Member, Now);
        db.Members.Add(member);
        await db.SaveChangesAsync();
        member.IsVotingEligible.Should().BeTrue("a Member seeds eligible, so turning it OFF is the real transition");
        var audit = Substitute.For<IAuditSink>();

        await new SetVotingEligibilityHandler(db, audit)
            .Handle(new SetVotingEligibilityCommand(member.PublicId, false), default);

        (await db.Members.AsNoTracking().SingleAsync()).IsVotingEligible.Should().BeFalse();
        await audit.Received(1).EmitEnrichedAsync(
            "Membership.VotingEligibilityChanged", nameof(CommitteeMember), member.PublicId.ToString(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Setting_eligibility_ON_works_for_a_role_that_does_not_seed_eligible()
    {
        await using var db = NewDb();
        // An Observer-style role seeds NOT eligible (only Chairman and Member do), so this is the
        // direction that actually grants a vote rather than removing one.
        var member = CommitteeMember.Provision("kc-2", "Reviewer Two", "v2@acmp.gov", CommitteeRole.Reviewer, Now);
        db.Members.Add(member);
        await db.SaveChangesAsync();
        member.IsVotingEligible.Should().BeFalse();

        await new SetVotingEligibilityHandler(db, Substitute.For<IAuditSink>())
            .Handle(new SetVotingEligibilityCommand(member.PublicId, true), default);

        (await db.Members.AsNoTracking().SingleAsync()).IsVotingEligible.Should().BeTrue();
    }

    // ⚠ A DISABLED MEMBER IS HISTORY, NOT A PARTICIPANT. Deactivation keeps the row so votes and
    // authorship stay attributed (AC-058); making one eligible would put somebody who cannot sign in
    // into the quorum arithmetic, counting toward a threshold nobody can then meet.
    [Fact]
    public async Task Setting_eligibility_is_REFUSED_for_a_member_who_is_not_active()
    {
        await using var db = NewDb();
        var member = CommitteeMember.Provision("kc-3", "Departed Three", "v3@acmp.gov", CommitteeRole.Member, Now);
        member.Deactivate();
        db.Members.Add(member);
        await db.SaveChangesAsync();
        var audit = Substitute.For<IAuditSink>();

        var act = () => new SetVotingEligibilityHandler(db, audit)
            .Handle(new SetVotingEligibilityCommand(member.PublicId, true), default);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("active", "a refusal that does not name its reason sends the reader to the wrong screen");
        (await db.Members.AsNoTracking().SingleAsync()).IsVotingEligible.Should().BeTrue(
            "the refusal must leave the stored value untouched, not half-apply it");
        await audit.DidNotReceive().EmitEnrichedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_unknown_member_is_a_not_found_rather_than_a_silent_no_op()
    {
        await using var db = NewDb();

        var act = () => new SetVotingEligibilityHandler(db, Substitute.For<IAuditSink>())
            .Handle(new SetVotingEligibilityCommand(Guid.NewGuid(), true), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public void Only_Chairman_and_Secretary_may_change_it()
    {
        // ⚠ ADMINISTRATOR IS ABSENT DELIBERATELY (DEC-046 d4). SoD-5 keeps that role out of committee
        // content, and who is eligible to vote is the most content-like decision on the roster —
        // including it would have crossed the separation and needed its own ADR.
        new SetVotingEligibilityCommand(Guid.NewGuid(), true).AllowedRoles
            .Should().BeEquivalentTo(new[] { "Chairman", "Secretary" });
    }

    [Fact]
    public void An_empty_member_id_is_refused_by_the_validator()
    {
        new SetVotingEligibilityValidator()
            .Validate(new SetVotingEligibilityCommand(Guid.Empty, true))
            .IsValid.Should().BeFalse();
    }

    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    private static MembershipDbContext NewDb()
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns("kc-chair");
        user.Roles.Returns(new[] { "Chairman" });
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);
        return new MembershipDbContext(
            new DbContextOptionsBuilder<MembershipDbContext>().UseInMemoryDatabase("vote-" + Guid.NewGuid()).Options,
            clock, user);
    }
}
