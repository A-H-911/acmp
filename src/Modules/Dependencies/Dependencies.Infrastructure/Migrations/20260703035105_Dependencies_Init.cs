using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Dependencies.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Dependencies_Init : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "dependencies");

        migrationBuilder.CreateTable(
            name: "dependencies",
            schema: "dependencies",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                Key = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                FromType = table.Column<int>(type: "int", nullable: false),
                FromId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FromKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                FromTitle = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                ToType = table.Column<int>(type: "int", nullable: false),
                ToId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ToKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                ToTitle = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                Kind = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_dependencies", x => x.Id);
                table.UniqueConstraint("AK_dependencies_PublicId", x => x.PublicId);
            });

        migrationBuilder.CreateTable(
            name: "dependency_key_counters",
            schema: "dependencies",
            columns: table => new
            {
                Prefix = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                Year = table.Column<int>(type: "int", nullable: false),
                Next = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_dependency_key_counters", x => new { x.Prefix, x.Year });
            });

        migrationBuilder.CreateIndex(
            name: "IX_dependencies_FromType_FromId",
            schema: "dependencies",
            table: "dependencies",
            columns: new[] { "FromType", "FromId" });

        migrationBuilder.CreateIndex(
            name: "IX_dependencies_Key",
            schema: "dependencies",
            table: "dependencies",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_dependencies_Status",
            schema: "dependencies",
            table: "dependencies",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_dependencies_ToType_ToId",
            schema: "dependencies",
            table: "dependencies",
            columns: new[] { "ToType", "ToId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "dependencies",
            schema: "dependencies");

        migrationBuilder.DropTable(
            name: "dependency_key_counters",
            schema: "dependencies");
    }
}
