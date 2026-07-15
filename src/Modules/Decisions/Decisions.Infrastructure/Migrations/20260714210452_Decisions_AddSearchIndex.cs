using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Decisions.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Decisions_AddSearchIndex : Migration
{
    // P15f (FR-143/145, AC-061): full-text index over the Decisions bilingual columns for global search.
    // Arabic word-breaker LCID 1025 (OQ-034 spike-confirmed) on the *_ar columns, English 1033 on *_en.
    //
    // Guarded + transaction-suppressed by design:
    //  - CREATE FULLTEXT CATALOG/INDEX cannot run inside a user transaction -> suppressTransaction: true.
    //  - Wrapped in IF SERVERPROPERTY('IsFullTextInstalled')=1 so it is a NO-OP on a stock SQL image (the
    //    SqlBackstopFixture's plain mssql container) and only builds on the FTS-enabled deploy image.
    //  - EXEC(...) runs the batch-restricted DDL in a child batch; NOT EXISTS makes it idempotent.
    //  - Per-module catalog (ft_decisions) so Down cleans up with no cross-module shared-catalog coupling.
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "IF SERVERPROPERTY('IsFullTextInstalled') = 1 " +
            "AND NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'ft_decisions') " +
            "EXEC('CREATE FULLTEXT CATALOG ft_decisions');",
            suppressTransaction: true);

        migrationBuilder.Sql(
            "IF SERVERPROPERTY('IsFullTextInstalled') = 1 " +
            "AND NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('decisions.decisions')) " +
            "EXEC('CREATE FULLTEXT INDEX ON decisions.decisions (" +
            "title_en LANGUAGE 1033, title_ar LANGUAGE 1025, " +
            "statement_en LANGUAGE 1033, statement_ar LANGUAGE 1025, " +
            "rationale_en LANGUAGE 1033, rationale_ar LANGUAGE 1025) " +
            "KEY INDEX PK_decisions ON ft_decisions WITH CHANGE_TRACKING = AUTO');",
            suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "IF SERVERPROPERTY('IsFullTextInstalled') = 1 " +
            "AND EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('decisions.decisions')) " +
            "EXEC('DROP FULLTEXT INDEX ON decisions.decisions');",
            suppressTransaction: true);

        migrationBuilder.Sql(
            "IF SERVERPROPERTY('IsFullTextInstalled') = 1 " +
            "AND EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'ft_decisions') " +
            "EXEC('DROP FULLTEXT CATALOG ft_decisions');",
            suppressTransaction: true);
    }
}
