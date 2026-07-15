using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Topics.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Topics_AddSearchIndex : Migration
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
            "AND NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'ft_topics') " +
            "EXEC('CREATE FULLTEXT CATALOG ft_topics');",
            suppressTransaction: true);

        migrationBuilder.Sql(
            "IF SERVERPROPERTY('IsFullTextInstalled') = 1 " +
            "AND NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('topics.topics')) " +
            "EXEC('CREATE FULLTEXT INDEX ON topics.topics (Title LANGUAGE 1025, Description LANGUAGE 1025) " +
            "KEY INDEX PK_topics ON ft_topics WITH CHANGE_TRACKING = AUTO');",
            suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "IF SERVERPROPERTY('IsFullTextInstalled') = 1 " +
            "AND EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('topics.topics')) " +
            "EXEC('DROP FULLTEXT INDEX ON topics.topics');",
            suppressTransaction: true);

        migrationBuilder.Sql(
            "IF SERVERPROPERTY('IsFullTextInstalled') = 1 " +
            "AND EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'ft_topics') " +
            "EXEC('DROP FULLTEXT CATALOG ft_topics');",
            suppressTransaction: true);
    }
}
