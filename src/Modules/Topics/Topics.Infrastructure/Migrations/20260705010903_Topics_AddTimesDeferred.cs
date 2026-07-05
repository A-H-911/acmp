using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Topics.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Topics_AddTimesDeferred : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "TimesDeferred",
            schema: "topics",
            table: "topics",
            type: "int",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "TimesDeferred",
            schema: "topics",
            table: "topics");
    }
}
