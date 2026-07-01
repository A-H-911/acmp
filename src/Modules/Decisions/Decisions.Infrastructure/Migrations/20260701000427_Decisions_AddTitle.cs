using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Decisions.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Decisions_AddTitle : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "title_ar",
            schema: "decisions",
            table: "decisions",
            type: "nvarchar(512)",
            maxLength: 512,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "title_en",
            schema: "decisions",
            table: "decisions",
            type: "nvarchar(512)",
            maxLength: 512,
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "title_ar",
            schema: "decisions",
            table: "decisions");

        migrationBuilder.DropColumn(
            name: "title_en",
            schema: "decisions",
            table: "decisions");
    }
}
