using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Knowledge.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Knowledge_AddTemplate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "templates",
            schema: "knowledge",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                Key = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                name_en = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                name_ar = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                TargetType = table.Column<int>(type: "int", nullable: false),
                Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Version = table.Column<int>(type: "int", nullable: false),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_templates", x => x.Id);
                table.UniqueConstraint("AK_templates_PublicId", x => x.PublicId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_templates_Key",
            schema: "knowledge",
            table: "templates",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_templates_Status",
            schema: "knowledge",
            table: "templates",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_templates_TargetType",
            schema: "knowledge",
            table: "templates",
            column: "TargetType");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "templates",
            schema: "knowledge");
    }
}
