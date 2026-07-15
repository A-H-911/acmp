using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Meetings.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Meetings_AddSearchIndex : Migration
{
    // P15f (FR-143/145, AC-061) — full-text index for global search. Guarded by IsFullTextInstalled (NO-OP on a
    // stock SQL image, builds only on the FTS-enabled deploy image), transaction-suppressed (FTS DDL cannot run
    // in a user transaction), EXEC'd (batch restriction) + NOT EXISTS (idempotent). Arabic word-breaker 1025 on
    // *_ar, English 1033 on *_en. Per-module catalog so Down needs no cross-module coupling.
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "IF SERVERPROPERTY('IsFullTextInstalled') = 1 " +
            "AND NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'ft_minutes') " +
            "EXEC('CREATE FULLTEXT CATALOG ft_minutes');",
            suppressTransaction: true);

        migrationBuilder.Sql(
            "IF SERVERPROPERTY('IsFullTextInstalled') = 1 " +
            "AND NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('meetings.minutes_of_meeting')) " +
            "EXEC('CREATE FULLTEXT INDEX ON meetings.minutes_of_meeting (summary_en LANGUAGE 1033, summary_ar LANGUAGE 1025) " +
            "KEY INDEX PK_minutes_of_meeting ON ft_minutes WITH CHANGE_TRACKING = AUTO');",
            suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "IF SERVERPROPERTY('IsFullTextInstalled') = 1 " +
            "AND EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('meetings.minutes_of_meeting')) " +
            "EXEC('DROP FULLTEXT INDEX ON meetings.minutes_of_meeting');",
            suppressTransaction: true);

        migrationBuilder.Sql(
            "IF SERVERPROPERTY('IsFullTextInstalled') = 1 " +
            "AND EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'ft_minutes') " +
            "EXEC('DROP FULLTEXT CATALOG ft_minutes');",
            suppressTransaction: true);
    }
}
