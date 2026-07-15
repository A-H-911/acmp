using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Knowledge.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Knowledge_AddSearchIndex : Migration
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
            "AND NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'ft_documents') " +
            "EXEC('CREATE FULLTEXT CATALOG ft_documents');",
            suppressTransaction: true);

        migrationBuilder.Sql(
            "IF SERVERPROPERTY('IsFullTextInstalled') = 1 " +
            "AND NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('knowledge.documents')) " +
            "EXEC('CREATE FULLTEXT INDEX ON knowledge.documents (title_en LANGUAGE 1033, title_ar LANGUAGE 1025, body_en LANGUAGE 1033, body_ar LANGUAGE 1025) " +
            "KEY INDEX PK_documents ON ft_documents WITH CHANGE_TRACKING = AUTO');",
            suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "IF SERVERPROPERTY('IsFullTextInstalled') = 1 " +
            "AND EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('knowledge.documents')) " +
            "EXEC('DROP FULLTEXT INDEX ON knowledge.documents');",
            suppressTransaction: true);

        migrationBuilder.Sql(
            "IF SERVERPROPERTY('IsFullTextInstalled') = 1 " +
            "AND EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'ft_documents') " +
            "EXEC('DROP FULLTEXT CATALOG ft_documents');",
            suppressTransaction: true);
    }
}
