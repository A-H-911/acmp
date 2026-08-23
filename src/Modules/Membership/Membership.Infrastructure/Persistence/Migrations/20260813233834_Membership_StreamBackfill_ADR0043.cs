using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Membership.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
// ADR-0043 step 5 (DEC-044) — the one-time backfill. Every member holding NO stream is assigned the
// wildcard, so that step 7 can wire StreamScopeRequirement without refusing the people who were
// seeded straight into Keycloak before the invite flow required a stream.
//
// ⚠ MUST RUN AFTER Membership_MemberStreamsNoIdentity_DEF066, and the ordering is the only reason
// that migration exists as a separate one: member_streams.StreamId shipped as an IDENTITY column, so
// the explicit insert below would be refused exactly as every application assignment was (DEF-066).
//
// ⚠ A MIGRATION TOUCHES ONLY THE ROWS THAT EXIST WHEN IT RUNS, which is the whole of its blast
// radius and is smaller than it looks (DEF-062, DEF-065). In every FRESH environment this is a
// no-op: Acmp.Api.Tests never migrates at all (InMemory), Acmp.Integration.Tests migrates an empty
// database, and the e2e stack migrates first and JIT-provisions its members afterwards. And in
// PRODUCTION it reaches only the accounts that have already signed in — a Keycloak account is not a
// committee_members row (DEF-065). Both are recorded; neither makes this migration wrong, and
// skipping it would only add the rows it DOES reach to the locked-out set.
public partial class Membership_StreamBackfill_ADR0043 : Migration
{
    /// <summary>
    /// The backfill itself, exposed so the integration test executes the statement that SHIPS rather
    /// than a copy of it — a second copy is how the test keeps passing after the migration changes.
    /// </summary>
    public const string BackfillSql = """
        -- ⚠ FAIL LOUDLY RATHER THAN QUIETLY DOING NOTHING. The ADR-0042 taxonomy seed inserts
        -- WHERE NOT EXISTS on Code, so a database that already carried an 'all-streams' row was
        -- SKIPPED and has no wildcard at all — its own comment says "verify after deploying". Without
        -- this check the backfill would silently assign nothing, look successful, and lock the
        -- committee out on the day step 7 lands, which is exactly the outcome it exists to prevent.
        IF NOT EXISTS (SELECT 1 FROM membership.streams WHERE IsWildcard = 1)
            THROW 50000, 'ADR-0043 step 5 backfill: no membership.streams row has IsWildcard = 1, so there is no wildcard to assign. The ADR-0042 seed skips codes that already exist, so a database that already carried an all-streams row has none. Insert the wildcard row and re-run before wiring StreamScopeRequirement.', 1;

        -- Idempotent by construction: after this runs every member holds at least one stream, so the
        -- NOT EXISTS excludes them all on any later execution. It is written that way for the
        -- operator who re-runs a half-finished deploy by hand, not for EF, which applies it once.
        -- ⚠ A member who ALREADY holds a stream is left exactly as they are — assigning the wildcard
        -- on top would silently widen a deliberate assignment into universal write access.
        -- ⚠ EVERY member with no streams is included, deliberately, including Guests. A Guest is
        -- bounded by a time window rather than by streams (permission-role-matrix E.3, DEF-060), so
        -- the row is noise on their roster line rather than a grant they did not have.
        INSERT INTO membership.member_streams (CommitteeMemberId, StreamId)
        SELECT m.Id, w.Id
        FROM membership.committee_members m
        CROSS JOIN (SELECT Id FROM membership.streams WHERE IsWildcard = 1) w
        WHERE NOT EXISTS (SELECT 1 FROM membership.member_streams ms WHERE ms.CommitteeMemberId = m.Id);
        """;

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(BackfillSql);

    /// <inheritdoc />
    // ⚠ DELIBERATELY A NO-OP, and it is not laziness about the rollback. The assignments this
    // migration writes are indistinguishable from ones an Administrator made through the UI
    // afterwards — member_streams carries no provenance column — so a Down that deleted wildcard
    // assignments would strip deliberate grants and lock those people out. Leaving them is also
    // consistent with the ADR-0042 seed's own Down, which refuses to delete a stream that any member
    // is assigned to. (Related, and one reason a dangling row would be silent: member_streams.StreamId
    // has no FK to streams — UserStreamProvider inner-joins, so an orphan simply disappears.)
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
