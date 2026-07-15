using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Governance.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Governance_AddSearchIndex : Migration
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
            "AND NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'ft_adrs') " +
            "EXEC('CREATE FULLTEXT CATALOG ft_adrs');",
            suppressTransaction: true);

        migrationBuilder.Sql(
            "IF SERVERPROPERTY('IsFullTextInstalled') = 1 " +
            "AND NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('governance.adrs')) " +
            "EXEC('CREATE FULLTEXT INDEX ON governance.adrs (title_en LANGUAGE 1033, title_ar LANGUAGE 1025, context_en LANGUAGE 1033, context_ar LANGUAGE 1025, decision_en LANGUAGE 1033, decision_ar LANGUAGE 1025) " +
            "KEY INDEX PK_adrs ON ft_adrs WITH CHANGE_TRACKING = AUTO');",
            suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "IF SERVERPROPERTY('IsFullTextInstalled') = 1 " +
            "AND EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('governance.adrs')) " +
            "EXEC('DROP FULLTEXT INDEX ON governance.adrs');",
            suppressTransaction: true);

        migrationBuilder.Sql(
            "IF SERVERPROPERTY('IsFullTextInstalled') = 1 " +
            "AND EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'ft_adrs') " +
            "EXEC('DROP FULLTEXT CATALOG ft_adrs');",
            suppressTransaction: true);
    }
}
