using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Application.Features.ReconcileIdentityAccounts;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Integration.Tests;

// S5 (ADR-0016 §3) — DEC-046 / DEF-065 / DEF-071 reconciliation, against REAL SQL SERVER.
//
// ⚠ WHY THIS SUITE AND NOT ONLY THE UNIT TESTS, WHICH ALREADY PASS. DEF-066: assigning a stream to a
// member had NEVER worked against a real database — member_streams.StreamId shipped as an IDENTITY
// column, so every explicit insert was refused — and two shipped steps of ADR-0043 were built on top
// of it with FOUR GREEN SUITES over a feature that could not work once. Acmp.Api.Tests runs InMemory
// (no identity columns, no filtered indexes, no FK behaviour), the domain tests use no database, and
// e2e never exercised the path. The lesson recorded from it is a question to ask before trusting any
// EF write path: HAS THIS EVER RUN AGAINST SQL SERVER? For this handler the answer would otherwise be
// no — and its write is not the same shape as the ones already proven. InviteUserHandler saves the
// member FIRST and assigns streams in a SECOND SaveChanges; this inserts the principal and its
// member_streams dependents in ONE, with StreamId an explicitly-set value on a non-identity column.
// That is the exact combination DEF-066 made unsafe, so it is proven here rather than assumed.
[Collection(SqlBackstopCollection.Name)]
public sealed class ReconcileIdentityAccountsSqlTests
{
    private readonly SqlBackstopFixture _fx;

    public ReconcileIdentityAccountsSqlTests(SqlBackstopFixture fx) => _fx = fx;

    [Fact]
    public async Task Reconciled_member_and_its_wildcard_assignment_both_persist_on_SQL_Server()
    {
        // Unique per run: the fixture's database is shared across the assembly, and the email column
        // carries a filtered unique index, so a fixed value would collide with a re-run rather than
        // fail on the property under test.
        var sub = $"kc-recon-{Guid.NewGuid():N}";
        var email = $"{sub}@acmp.gov";

        long wildcardId;
        await using (var db = _fx.NewMembershipSql())
            wildcardId = await db.Streams.Where(s => s.IsWildcard).Select(s => s.Id).SingleAsync();

        IdentityReconciliationResult result;
        await using (var db = _fx.NewMembershipSql())
        {
            var handler = new ReconcileIdentityAccountsHandler(
                db, _fx.Clock, Substitute.For<IAuditSink>(),
                new[] { Realm(new IdentityAccount(sub, email, "Reconciled Member", true, new[] { "Member" })) });

            result = await handler.Handle(new ReconcileIdentityAccountsCommand(), default);
        }

        result.Created.Should().Be(1);

        // Read back through a FRESH context — the assertion must survive the round trip to SQL Server
        // rather than reading the change tracker that produced it.
        await using (var verify = _fx.NewMembershipSql())
        {
            var member = await verify.Members.AsNoTracking().SingleAsync(m => m.KeycloakUserId == sub);
            member.Status.Should().Be(MembershipStatus.Invited);
            member.Streams.Select(s => s.StreamId).Should().ContainSingle().Which.Should().Be(wildcardId,
                "the stored assignment must be the wildcard that was chosen — an id the database generated instead would scope the member to whatever row that number happens to be (DEF-066)");
        }
    }

    // The DEF-071 asymmetry, proven where it would actually be enforced. An existing member is matched
    // by SUBJECT, so a realm listing that includes them must leave their narrower assignment untouched.
    [Fact]
    public async Task An_existing_member_narrowed_to_one_stream_is_not_widened_on_SQL_Server()
    {
        var sub = $"kc-narrow-{Guid.NewGuid():N}";
        var email = $"{sub}@acmp.gov";

        long coreId;
        await using (var db = _fx.NewMembershipSql())
        {
            coreId = await db.Streams.Where(s => s.Code == "core").Select(s => s.Id).SingleAsync();
            var member = CommitteeMember.Provision(sub, "Narrowed Member", email, CommitteeRole.Member, _fx.Clock.UtcNow);
            member.AssignStreams(new[] { coreId });
            db.Members.Add(member);
            await db.SaveChangesAsync();
        }

        await using (var db = _fx.NewMembershipSql())
        {
            var handler = new ReconcileIdentityAccountsHandler(
                db, _fx.Clock, Substitute.For<IAuditSink>(),
                new[] { Realm(new IdentityAccount(sub, email, "Narrowed Member", true, new[] { "Member" })) });

            var result = await handler.Handle(new ReconcileIdentityAccountsCommand(), default);
            result.Created.Should().Be(0);
            result.AlreadyProvisioned.Should().BeGreaterThan(0);
        }

        await using (var verify = _fx.NewMembershipSql())
        {
            var member = await verify.Members.AsNoTracking().SingleAsync(m => m.KeycloakUserId == sub);
            member.Streams.Select(s => s.StreamId).Should().ContainSingle().Which.Should().Be(coreId,
                "an administrator's deliberate single-stream scope must survive a reconciliation run");
        }
    }

    private static IIdentityProvider Realm(params IdentityAccount[] accounts)
    {
        var identity = Substitute.For<IIdentityProvider>();
        identity.ListUsersAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IdentityAccount>>(accounts));
        return identity;
    }
}
