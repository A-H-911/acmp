using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Governance.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Governance_InvariantInit : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "invariants",
            schema: "governance",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                Key = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                Category = table.Column<int>(type: "int", nullable: false),
                Scope = table.Column<int>(type: "int", nullable: false),
                statement_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                statement_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                rationale_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                rationale_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                exceptions_policy_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                exceptions_policy_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                OwnerUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                OwnerName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                ActivatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ActivatedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                ActivatedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                SupersededByInvariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                supersession_reason_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                supersession_reason_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                SupersedesInvariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                retirement_reason_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                retirement_reason_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_invariants", x => x.Id);
                table.UniqueConstraint("AK_invariants_PublicId", x => x.PublicId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_invariants_Key",
            schema: "governance",
            table: "invariants",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_invariants_Status",
            schema: "governance",
            table: "invariants",
            column: "Status");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "invariants",
            schema: "governance");
    }
}
