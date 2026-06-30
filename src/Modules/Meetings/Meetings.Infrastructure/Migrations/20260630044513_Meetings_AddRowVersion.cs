using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Meetings.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Meetings_AddRowVersion : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            schema: "meetings",
            table: "meetings",
            type: "rowversion",
            rowVersion: true,
            nullable: false,
            defaultValue: new byte[0]);

        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            schema: "meetings",
            table: "agendas",
            type: "rowversion",
            rowVersion: true,
            nullable: false,
            defaultValue: new byte[0]);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RowVersion",
            schema: "meetings",
            table: "meetings");

        migrationBuilder.DropColumn(
            name: "RowVersion",
            schema: "meetings",
            table: "agendas");
    }
}
