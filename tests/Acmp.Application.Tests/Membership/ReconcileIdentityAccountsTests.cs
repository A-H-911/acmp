using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Application.Features.ReconcileIdentityAccounts;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Stream = Acmp.Modules.Membership.Domain.Stream;

namespace Acmp.Application.Tests.Membership;

// DEC-046 / DEF-065 / DEF-071 — reconciling identity-provider accounts into committee_members.
//
// EVERY GUARD IS PROVEN BY FORCING ITS REFUSAL. The two that matter most are not the happy path:
// the command must NOT widen a member who already holds a narrower stream (DEF-071's asymmetry), and
// it must refuse BEFORE writing anything when there is no wildcard to grant — because creating the
// rows anyway, and reporting success, produces exactly the locked-out members this exists to prevent.
public class ReconcileIdentityAccountsTests
{
    private const string Wildcard = "all-streams";

    // ---- the core claim ----

    [Fact]
    public async Task Reconcile_creates_the_missing_member_holding_the_wildcard_and_audits_it()
    {
        await using var db = NewDb();
        var wildcardId = await SeedStreamsAsync(db);
        var identity = Realm(Account("kc-unseen", "unseen@acmp.gov", "Unseen Member", "Member"));
        var audit = Substitute.For<IAuditSink>();

        var result = await Handler(db, identity, audit).Handle(new ReconcileIdentityAccountsCommand(), default);

        result.Created.Should().Be(1);
        var member = await db.Members.SingleAsync(m => m.KeycloakUserId == "kc-unseen");
        member.Status.Should().Be(MembershipStatus.Invited,
            "the account exists in Keycloak and has never signed in — first login flips it to Active through SyncFromClaims");
        member.Role.Should().Be(CommitteeRole.Member, "the role is the claims-derived cache, seeded from the realm roles");

        // ⚠ THE WHOLE POINT (DEF-071). Creating the row is necessary and NOT sufficient: the step-5
        // backfill has already run by the time this command can, so a row created here with no stream
        // is refused every guarded write from its owner's first login.
        member.Streams.Select(s => s.StreamId).Should().ContainSingle().Which.Should().Be(wildcardId,
            "the reconciliation must do what the backfill would have done had the row existed");

        await audit.Received(1).EmitEnrichedAsync(
            "Membership.AccountReconciled", nameof(CommitteeMember), member.PublicId.ToString(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ⚠ THE DISCRIMINATING TEST. A command that assigned the wildcard to everyone it saw would pass
    // every other test in this file and silently widen a deliberate single-stream assignment into
    // universal write access — the control would be decorative from its first day.
    [Fact]
    public async Task Reconcile_leaves_an_already_provisioned_member_and_their_narrower_stream_alone()
    {
        await using var db = NewDb();
        var wildcardId = await SeedStreamsAsync(db);
        var coreId = await db.Streams.Where(s => s.Code == "core").Select(s => s.Id).SingleAsync();

        var narrowed = CommitteeMember.Provision("kc-core", "Core Member", "core@acmp.gov", CommitteeRole.Member, Now);
        narrowed.AssignStreams(new[] { coreId });
        db.Members.Add(narrowed);
        await db.SaveChangesAsync();

        var identity = Realm(Account("kc-core", "core@acmp.gov", "Core Member", "Member"));

        var result = await Handler(db, identity).Handle(new ReconcileIdentityAccountsCommand(), default);

        result.Created.Should().Be(0);
        result.AlreadyProvisioned.Should().Be(1);
        var member = await db.Members.AsNoTracking().SingleAsync(m => m.KeycloakUserId == "kc-core");
        member.Streams.Select(s => s.StreamId).Should().ContainSingle().Which.Should().Be(coreId,
            "an existing assignment is a deliberate scope; widening it to the wildcard would hand universal write access to someone an administrator narrowed on purpose");
        member.Streams.Select(s => s.StreamId).Should().NotContain(wildcardId);
    }

    // ⚠ THE RESIDUAL, PINNED AS INTENDED BEHAVIOUR RATHER THAN LEFT AS AN ACCIDENT — and it is the
    // ONE case the test above cannot discriminate. A member who signs in BETWEEN the stream-scope
    // deploy and this run holds a JIT row with NO streams, and this command leaves it alone, because
    // it is no longer a row this run creates. Read cold that looks like the bug the command exists to
    // fix, so the next person will be tempted to "correct" it to "wildcard any member holding zero
    // streams" — which is wrong for a reason that is invisible from the data: an administrator can
    // clear a member's streams deliberately, and member_streams carries no provenance column to tell
    // the two apart. The remedy for the person in this window is ADR-0043 clause (2)'s roster
    // backstop, not a widening here that would silently re-grant everyone an administrator revoked.
    [Fact]
    public async Task Reconcile_leaves_a_pre_existing_member_holding_NO_streams_alone()
    {
        await using var db = NewDb();
        await SeedStreamsAsync(db);
        // Exactly what JIT provisioning produces: CommitteeMember.Provision touches no stream
        // collection (ADR-0004), so this row arrives holding nothing.
        db.Members.Add(CommitteeMember.Provision("kc-jit", "Signed In First", "jit@acmp.gov", CommitteeRole.Member, Now));
        await db.SaveChangesAsync();

        var identity = Realm(Account("kc-jit", "jit@acmp.gov", "Signed In First", "Member"));

        var result = await Handler(db, identity).Handle(new ReconcileIdentityAccountsCommand(), default);

        result.Created.Should().Be(0);
        result.AlreadyProvisioned.Should().Be(1);
        var member = await db.Members.AsNoTracking().SingleAsync(m => m.KeycloakUserId == "kc-jit");
        member.Streams.Should().BeEmpty(
            "the rule is 'only the rows this run creates', not 'any member holding no streams' — the second would re-widen anyone an administrator deliberately cleared, and nothing in the data distinguishes them");
    }

    // ---- the refusals ----

    [Fact]
    public async Task Reconcile_refuses_and_writes_NOTHING_when_the_database_has_no_wildcard_stream()
    {
        await using var db = NewDb();
        // The state the ADR-0042 seed's own comment warns about: a database that already carried an
        // 'all-streams' code is skipped by that seed and reaches here with no wildcard at all.
        db.Streams.Add(Stream.Create("core", LocalizedString.Create("Core", "الأساسي")));
        await db.SaveChangesAsync();
        var identity = Realm(Account("kc-unseen", "unseen@acmp.gov", "Unseen Member", "Member"));

        var act = () => Handler(db, identity).Handle(new ReconcileIdentityAccountsCommand(), default);

        // Detect AND tell: a refusal whose message does not name the missing thing sends the operator
        // to the wrong file.
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("IsWildcard");
        db.Members.Should().BeEmpty(
            "refusing after creating half the rows would leave exactly the stream-less members the command exists to prevent");
    }

    [Fact]
    public async Task Reconcile_refuses_when_no_identity_provider_is_configured_and_names_BOTH_variables()
    {
        await using var db = NewDb();
        await SeedStreamsAsync(db);

        var handler = new ReconcileIdentityAccountsHandler(
            db, Clock(), Substitute.For<IAuditSink>(), Array.Empty<IIdentityProvider>());

        var act = () => handler.Handle(new ReconcileIdentityAccountsCommand(), default);

        // DEC-047 d3: enabling in-app user management is TWO variables, and the operator is to be told
        // so truthfully rather than given the wrong reason.
        var message = (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message;
        message.Should().Contain("KeycloakAdmin:Enabled").And.Contain("KeycloakAdmin:ClientSecret");
    }

    // ---- the report ----

    // Every account the provider returned lands in exactly ONE bucket and the buckets sum to the
    // total. A bare "created" count cannot be told apart from a run that silently skipped half the
    // realm — which is the shape of every control this project has had to fix twice.
    [Fact]
    public async Task Reconcile_reports_every_account_in_exactly_one_bucket()
    {
        await using var db = NewDb();
        await SeedStreamsAsync(db);
        db.Members.Add(CommitteeMember.Provision("kc-known", "Known", "known@acmp.gov", CommitteeRole.Member, Now));
        await db.SaveChangesAsync();

        var identity = Realm(
            Account("kc-known", "known@acmp.gov", "Known", "Member"),
            Account("kc-new", "new@acmp.gov", "New Person", "Reviewer"),
            Account("kc-off", "off@acmp.gov", "Left The Committee", enabled: false, roles: "Member"),
            // The service account and the bootstrap admin look exactly like this: real accounts that
            // hold no committee role and could not have used the application either way.
            Account("kc-service", "service@acmp.gov", "Service Account"),
            // Same person, second account — DEF-045's duplicate identities. Email is uniquely indexed
            // where non-empty, so creating this would throw and abandon the WHOLE run.
            Account("kc-dupe", "new@acmp.gov", "New Person Again", "Member"));

        var result = await Handler(db, identity).Handle(new ReconcileIdentityAccountsCommand(), default);

        result.Should().BeEquivalentTo(new IdentityReconciliationResult(
            IdentityAccounts: 5, Created: 1, AlreadyProvisioned: 1,
            SkippedDisabled: 1, SkippedNoCommitteeRole: 1, SkippedDuplicateEmail: 1));

        (result.Created + result.AlreadyProvisioned + result.SkippedDisabled +
         result.SkippedNoCommitteeRole + result.SkippedDuplicateEmail)
            .Should().Be(result.IdentityAccounts, "the buckets must partition the realm, or a skipped account is invisible");

        (await db.Members.CountAsync()).Should().Be(2, "only the one reconcilable account becomes a member");
    }

    [Fact]
    public async Task Reconcile_run_twice_creates_nothing_the_second_time()
    {
        await using var db = NewDb();
        await SeedStreamsAsync(db);
        var identity = Realm(Account("kc-unseen", "unseen@acmp.gov", "Unseen Member", "Member"));

        (await Handler(db, identity).Handle(new ReconcileIdentityAccountsCommand(), default)).Created.Should().Be(1);
        var second = await Handler(db, identity).Handle(new ReconcileIdentityAccountsCommand(), default);

        second.Created.Should().Be(0);
        second.AlreadyProvisioned.Should().Be(1);
        (await db.Members.CountAsync()).Should().Be(1, "a second run must be a no-op, not a second row");
    }

    [Fact]
    public void Reconcile_is_Administrator_only()
    {
        // Administrator alone, matching PUT /api/members/{id}/streams — this writes what that writes.
        // Administrator is also not stream-bounded, so the command stays reachable in the very deploy
        // that starts refusing unassigned members.
        new ReconcileIdentityAccountsCommand().AllowedRoles.Should().BeEquivalentTo(new[] { "Administrator" });
    }

    // ---- helpers ----

    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    private static MembershipDbContext NewDb()
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns("kc-admin");
        user.Roles.Returns(new[] { "Administrator" });
        return new MembershipDbContext(
            new DbContextOptionsBuilder<MembershipDbContext>().UseInMemoryDatabase("reconcile-" + Guid.NewGuid()).Options,
            Clock(), user);
    }

    private static IClock Clock()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);
        return clock;
    }

    /// <summary>The taxonomy as the ADR-0042 migration seeds it, minus the four streams nothing here reads.</summary>
    private static async Task<long> SeedStreamsAsync(MembershipDbContext db)
    {
        db.Streams.Add(Stream.Create("core", LocalizedString.Create("Core", "الأساسي")));
        // The wildcard is a seeded singleton with no domain factory (the migration sets the column),
        // so the flag is set the only way a test can — the same idiom as MembershipResolverTests.
        var wildcard = Stream.Create(Wildcard, LocalizedString.Create("All streams", "كل المسارات"));
        typeof(Stream).GetProperty(nameof(Stream.IsWildcard))!.SetValue(wildcard, true);
        db.Streams.Add(wildcard);
        await db.SaveChangesAsync();
        return wildcard.Id;
    }

    private static ReconcileIdentityAccountsHandler Handler(
        MembershipDbContext db, IIdentityProvider identity, IAuditSink? audit = null) =>
        new(db, Clock(), audit ?? Substitute.For<IAuditSink>(), new[] { identity });

    private static IIdentityProvider Realm(params IdentityAccount[] accounts)
    {
        var identity = Substitute.For<IIdentityProvider>();
        identity.ListUsersAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IdentityAccount>>(accounts));
        return identity;
    }

    private static IdentityAccount Account(
        string subjectId, string email, string fullName, params string[] roles) =>
        new(subjectId, email, fullName, Enabled: true, roles);

    private static IdentityAccount Account(
        string subjectId, string email, string fullName, bool enabled, params string[] roles) =>
        new(subjectId, email, fullName, enabled, roles);
}
