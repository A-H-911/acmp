using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Membership.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
// ADR-0042 step 1: seed the committee's delivery streams and the wildcard row. Until this runs the
// streams table is EMPTY (Stream.Create has never had a caller — DEF-057), so no member can hold a
// stream and the stream-scope control cannot work at all. This must land BEFORE the requirement is
// wired into any policy (step 7), or every stream-bounded role is denied every topic.
public partial class Membership_StreamTaxonomy_ADR0042 : Migration
{
    // Fixed so every environment agrees. AssignStreams resolves PublicId per-database and
    // authorization keys on Code, so divergence would not break either — but identical ids make a
    // row trivially comparable across prod/UAT/CI when diagnosing a refusal.
    private const string SeedGuidPrefix = "3f7a1c92-4b8e-4d51-9f26-0a1b2c3d4e";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsWildcard",
            schema: "membership",
            table: "streams",
            type: "bit",
            nullable: false,
            defaultValue: false);

        // At most one wildcard, enforced by the database (ADR-0042 §3). Created BEFORE the seed so it
        // guards the seed's own insert rather than merely everything after it.
        migrationBuilder.CreateIndex(
            name: "IX_streams_IsWildcard",
            schema: "membership",
            table: "streams",
            column: "IsWildcard",
            unique: true,
            filter: "[IsWildcard] = 1");

        // ⚠ IDEMPOTENT ON PURPOSE, and it is not defensive padding. IX_streams_Code is UNIQUE, so a
        // plain InsertData against a database that already carries a hand-inserted 'core' row fails
        // the migration and takes the deployment down. Every record we have says production's streams
        // table is empty — but those are files describing a deployed state, and this project has been
        // burned by trusting exactly that. NOT EXISTS makes the belief irrelevant instead of checked.
        // ⚠ A pre-existing row is SKIPPED, never updated: silently flipping someone else's row to
        // IsWildcard = 1 would hand out universal write access. Skipping is the safe direction, so a
        // database that already had these codes ends up WITHOUT a wildcard — verify after deploying.
        migrationBuilder.Sql($"""
            INSERT INTO membership.streams (Code, name_en, name_ar, PublicId, CreatedAt, CreatedBy, IsWildcard)
            SELECT v.Code, v.NameEn, v.NameAr, CONVERT(uniqueidentifier, v.PublicId),
                   CONVERT(datetimeoffset, '2026-08-13T00:00:00+00:00'), 'seed:ADR-0042', CONVERT(bit, v.IsWildcard)
            FROM (VALUES
                ('core',            N'Core',            N'الأساسي',            '{SeedGuidPrefix}01', 0),
                ('communications',  N'Communications',  N'الاتصالات',          '{SeedGuidPrefix}02', 0),
                ('smart-cities',    N'Smart Cities',    N'المدن الذكية',       '{SeedGuidPrefix}03', 0),
                ('government',      N'Government',      N'الحكومي',            '{SeedGuidPrefix}04', 0),
                ('shared-services', N'Shared Services', N'الخدمات المشتركة',   '{SeedGuidPrefix}05', 0),
                -- The wildcard. Its wording is the committee's own, reused verbatim from the existing
                -- i18n key reports.filter.allStreams so the roster and the reports filter agree.
                ('all-streams',     N'All streams',     N'كل المسارات',        '{SeedGuidPrefix}ff', 1)
            ) AS v (Code, NameEn, NameAr, PublicId, IsWildcard)
            WHERE NOT EXISTS (SELECT 1 FROM membership.streams s WHERE s.Code = v.Code);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Only the rows this migration seeded, and only if still untouched — a stream an
        // administrator has since assigned to a member is not ours to delete on a rollback.
        migrationBuilder.Sql("""
            DELETE FROM membership.streams
            WHERE CreatedBy = 'seed:ADR-0042'
              AND NOT EXISTS (SELECT 1 FROM membership.member_streams ms WHERE ms.StreamId = streams.Id);
            """);

        migrationBuilder.DropIndex(
            name: "IX_streams_IsWildcard",
            schema: "membership",
            table: "streams");

        migrationBuilder.DropColumn(
            name: "IsWildcard",
            schema: "membership",
            table: "streams");
    }
}
