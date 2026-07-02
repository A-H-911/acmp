using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Decisions.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Decisions_AddStatement : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "statement_ar",
            schema: "decisions",
            table: "decisions",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "statement_en",
            schema: "decisions",
            table: "decisions",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "statement_ar",
            schema: "decisions",
            table: "decisions");

        migrationBuilder.DropColumn(
            name: "statement_en",
            schema: "decisions",
            table: "decisions");
    }
}
