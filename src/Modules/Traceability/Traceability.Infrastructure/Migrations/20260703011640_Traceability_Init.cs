using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Traceability.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Traceability_Init : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "traceability");

        migrationBuilder.CreateTable(
            name: "relationships",
            schema: "traceability",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                SourceType = table.Column<int>(type: "int", nullable: false),
                SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                SourceTitle = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                TargetType = table.Column<int>(type: "int", nullable: false),
                TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TargetKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                TargetTitle = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                RelType = table.Column<int>(type: "int", nullable: false),
                Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                DeactivatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                DeactivatedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_relationships", x => x.Id);
                table.UniqueConstraint("AK_relationships_PublicId", x => x.PublicId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_relationships_RelType",
            schema: "traceability",
            table: "relationships",
            column: "RelType",
            filter: "[IsActive] = 1");

        migrationBuilder.CreateIndex(
            name: "IX_relationships_SourceType_SourceId",
            schema: "traceability",
            table: "relationships",
            columns: new[] { "SourceType", "SourceId" },
            filter: "[IsActive] = 1");

        migrationBuilder.CreateIndex(
            name: "IX_relationships_TargetType_TargetId",
            schema: "traceability",
            table: "relationships",
            columns: new[] { "TargetType", "TargetId" },
            filter: "[IsActive] = 1");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "relationships",
            schema: "traceability");
    }
}
