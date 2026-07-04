using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Governance.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Governance_Init : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "governance");

        migrationBuilder.CreateTable(
            name: "adr_key_counters",
            schema: "governance",
            columns: table => new
            {
                Prefix = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                Year = table.Column<int>(type: "int", nullable: false),
                Next = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_adr_key_counters", x => new { x.Prefix, x.Year });
            });

        migrationBuilder.CreateTable(
            name: "adrs",
            schema: "governance",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                Key = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                title_en = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                title_ar = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                context_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                context_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                decision_drivers_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                decision_drivers_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                decision_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                decision_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                consequences_positive_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                consequences_positive_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                consequences_negative_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                consequences_negative_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                AuthorUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                AuthorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                SourceDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ApprovedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                ApprovedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                SupersededByAdrId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                supersession_reason_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                supersession_reason_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                SupersedesAdrId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                deprecation_reason_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                deprecation_reason_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_adrs", x => x.Id);
                table.UniqueConstraint("AK_adrs_PublicId", x => x.PublicId);
            });

        migrationBuilder.CreateTable(
            name: "adr_options",
            schema: "governance",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                name_en = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                name_ar = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                body_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                body_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                IsChosen = table.Column<bool>(type: "bit", nullable: false),
                AdrEntityId = table.Column<long>(type: "bigint", nullable: false),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_adr_options", x => x.Id);
                table.ForeignKey(
                    name: "FK_adr_options_adrs_AdrEntityId",
                    column: x => x.AdrEntityId,
                    principalSchema: "governance",
                    principalTable: "adrs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_adr_options_AdrEntityId",
            schema: "governance",
            table: "adr_options",
            column: "AdrEntityId");

        migrationBuilder.CreateIndex(
            name: "IX_adr_options_PublicId",
            schema: "governance",
            table: "adr_options",
            column: "PublicId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_adrs_Key",
            schema: "governance",
            table: "adrs",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_adrs_Status",
            schema: "governance",
            table: "adrs",
            column: "Status");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "adr_key_counters",
            schema: "governance");

        migrationBuilder.DropTable(
            name: "adr_options",
            schema: "governance");

        migrationBuilder.DropTable(
            name: "adrs",
            schema: "governance");
    }
}
