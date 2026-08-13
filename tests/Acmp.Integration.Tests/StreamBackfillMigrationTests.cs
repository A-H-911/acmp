using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Modules.Membership.Infrastructure.Persistence.Migrations;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Acmp.Integration.Tests;

// S5 (ADR-0016 §3) — ADR-0043 step 5, the backfill migration (DEC-044).
//
// ⚠ EVERY TEST HERE RUNS ON ITS OWN DATABASE, AND THAT IS THE POINT RATHER THAN HYGIENE. The claim
// under test is "the migration assigns the wildcard to members that ALREADY EXIST when it runs", and
// the fixture's shared database was migrated to head before any test could insert a member — so
// against it the backfill is a no-op that would pass while proving nothing (DEF-062: in every fresh
// environment this migration touches zero rows). The only way to exercise the real claim is to
// migrate PART WAY, create the rows, then migrate the rest — which needs a database of its own.
[Collection(SqlBackstopCollection.Name)]
public sealed class StreamBackfillMigrationTests
{
    // The migration immediately BEFORE the backfill — the state of a committee that already has
    // members and stream assignments on the day the backfill is deployed. Named explicitly rather
    // than computed: "the one before the last" would silently follow any migration added later, and
    // the point of stopping here is that the backfill has not run yet.
    private const string PreBackfill = "20260813233752_Membership_MemberStreamsNoIdentity_DEF066";

    // The migration before the taxonomy seed — used to set up the one database that must reach the
    // backfill with NO wildcard row in it.
    private const string BeforeTaxonomySeed = "20260812093823_Membership_PrincipalRevalidation_ADR0039";

    private const string WildcardCode = "all-streams";

    private readonly SqlBackstopFixture _fx;

    public StreamBackfillMigrationTests(SqlBackstopFixture fx) => _fx = fx;

    // The claim DEF-062 says everyone gets wrong, proven through the real mechanism: rows that exist
    // when the migration runs are backfilled, and rows that already carry a stream are not touched.
    [Fact]
    public async Task Backfill_assigns_the_wildcard_only_to_members_holding_no_streams()
    {
        var connectionString = await NewDatabaseAsync("adr0043_backfill");

        long coreStreamId;
        await using (var db = NewContext(connectionString))
        {
            await MigrateToAsync(db, PreBackfill);

            // A committee that pre-dates stream scope: nobody was ever assigned anything…
            db.Members.Add(CommitteeMember.Provision(
                "sub-unassigned", "Unassigned Member", "unassigned@acmp.gov", CommitteeRole.Member, _fx.Clock.UtcNow));

            // …except one person an administrator had already put on a single delivery stream.
            coreStreamId = await db.Streams.Where(s => s.Code == "core").Select(s => s.Id).SingleAsync();
            var assigned = CommitteeMember.Provision(
                "sub-core", "Core Member", "core@acmp.gov", CommitteeRole.Reviewer, _fx.Clock.UtcNow);
            assigned.AssignStreams(new[] { coreStreamId });
            db.Members.Add(assigned);

            await db.SaveChangesAsync();
        }

        // Act — the rest of the migrations, which is where the backfill runs.
        await using (var db = NewContext(connectionString))
            await MigrateToAsync(db, target: null);

        await using (var db = NewContext(connectionString))
        {
            var wildcardId = await db.Streams.Where(s => s.IsWildcard).Select(s => s.Id).SingleAsync();

            var unassigned = await db.Members.AsNoTracking().SingleAsync(m => m.KeycloakUserId == "sub-unassigned");
            unassigned.Streams.Select(s => s.StreamId).Should().ContainSingle().Which.Should().Be(wildcardId,
                "a member holding nothing must end up unrestricted rather than refused once step 7 wires the requirement");

            // ⚠ The discriminating half. A backfill that assigned the wildcard to EVERYONE would pass
            // the assertion above and silently widen a deliberate single-stream assignment into
            // universal write access — the control would be decorative from its first day.
            var core = await db.Members.AsNoTracking().SingleAsync(m => m.KeycloakUserId == "sub-core");
            core.Streams.Select(s => s.StreamId).Should().ContainSingle().Which.Should().Be(coreStreamId,
                "an existing assignment is a deliberate scope and the backfill must not widen it");
        }

        // Re-running the shipped statement changes nothing and throws nothing. This is the operator
        // re-running a half-finished deploy by hand — EF itself applies a migration only once — and
        // without the NOT EXISTS it would violate the (member, stream) primary key.
        await using (var db = NewContext(connectionString))
        {
            var rerun = () => db.Database.ExecuteSqlRawAsync(Membership_StreamBackfill_ADR0043.BackfillSql);
            await rerun.Should().NotThrowAsync();
        }

        await using (var db = NewContext(connectionString))
        {
            var unassigned = await db.Members.AsNoTracking().SingleAsync(m => m.KeycloakUserId == "sub-unassigned");
            unassigned.Streams.Should().HaveCount(1, "a second run must be a no-op, not a second assignment");
        }
    }

    // The failure the ADR-0042 seed's own comment warns about, end to end: a database that already
    // carried an 'all-streams' code is SKIPPED by that seed and therefore reaches this migration with
    // no wildcard at all. Assigning nothing and reporting success is the shape that produces the
    // lockout, so the migration must stop the deployment and SAY WHY.
    [Fact]
    public async Task Backfill_fails_loudly_when_the_database_has_no_wildcard_stream()
    {
        var connectionString = await NewDatabaseAsync("adr0043_nowildcard");

        await using (var db = NewContext(connectionString))
        {
            await MigrateToAsync(db, BeforeTaxonomySeed);

            // Raw SQL, because at this migration the IsWildcard column does not exist yet — which is
            // precisely the pre-existing row the taxonomy seed then declines to overwrite.
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO membership.streams (Code, name_en, name_ar, PublicId, CreatedAt, CreatedBy) " +
                "VALUES ({0}, N'All streams', N'كل المسارات', NEWID(), SYSDATETIMEOFFSET(), 'hand-inserted')",
                WildcardCode);
        }

        await using (var migrated = NewContext(connectionString))
        {
            var migrate = () => MigrateToAsync(migrated, target: null);

            // Detect AND tell: a thrown exception whose message does not name the missing thing sends
            // the operator to the wrong file.
            (await migrate.Should().ThrowAsync<Exception>())
                .Which.Message.Should().Contain("wildcard");
        }
    }

    // The production shape of an assignment — AssignStreamsHandler (PUT /api/members/{id}/streams) and
    // InviteUserHandler both load an EXISTING member, call AssignStreams with ids resolved from the
    // streams table, and SaveChanges. Proven here rather than in Acmp.Api.Tests because the InMemory
    // provider has no identity columns: it accepts an explicitly-set key that SQL Server refuses, which
    // is the contrast this whole suite exists to draw.
    [Fact]
    public async Task Assigning_a_stream_to_an_existing_member_stores_the_chosen_stream()
    {
        var sub = $"sub-assign-{Guid.NewGuid():N}";
        await using (var db = _fx.NewMembershipSql())
        {
            db.Members.Add(CommitteeMember.Provision(
                sub, "Assignable Member", $"{sub}@acmp.gov", CommitteeRole.Member, _fx.Clock.UtcNow));
            await db.SaveChangesAsync();
        }

        long coreStreamId;
        await using (var db = _fx.NewMembershipSql())
        {
            coreStreamId = await db.Streams.Where(s => s.Code == "core").Select(s => s.Id).SingleAsync();
            var member = await db.Members.SingleAsync(m => m.KeycloakUserId == sub);
            member.AssignStreams(new[] { coreStreamId });
            await db.SaveChangesAsync();
        }

        await using (var verify = _fx.NewMembershipSql())
        {
            var member = await verify.Members.AsNoTracking().SingleAsync(m => m.KeycloakUserId == sub);
            member.Streams.Select(s => s.StreamId).Should().ContainSingle().Which.Should().Be(coreStreamId,
                "the stored assignment must be the stream that was chosen — an id the database generated instead would scope the member to whatever row that number happens to be");
        }
    }

    // ---- helpers ----

    private async Task<string> NewDatabaseAsync(string prefix)
    {
        var name = $"{prefix}_{Guid.NewGuid():N}"[..48];
        await using var master = new SqlConnection(_fx.ConnectionString);
        await master.OpenAsync();
        await using var cmd = master.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE [{name}]";
        await cmd.ExecuteNonQueryAsync();

        return new SqlConnectionStringBuilder(_fx.ConnectionString) { InitialCatalog = name }.ConnectionString;
    }

    private MembershipDbContext NewContext(string connectionString) => new(
        new DbContextOptionsBuilder<MembershipDbContext>()
            .UseSqlServer(connectionString,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", MembershipDbContext.Schema))
            .Options,
        _fx.Clock, _fx.CurrentUser);

    // MigrateAsync() applies everything; IMigrator is the only seam that stops at a named migration,
    // and stopping part way is what lets rows exist before the backfill runs.
    private static Task MigrateToAsync(DbContext db, string? target) =>
        db.GetService<IMigrator>().MigrateAsync(target);
}
