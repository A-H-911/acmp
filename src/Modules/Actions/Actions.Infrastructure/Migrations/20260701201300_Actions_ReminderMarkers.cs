using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Actions.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Actions_ReminderMarkers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DueReminderSentAt",
            schema: "actions",
            table: "actions",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "EscalatedToChairmanAt",
            schema: "actions",
            table: "actions",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "EscalatedToSecretaryAt",
            schema: "actions",
            table: "actions",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "OverdueNotifiedAt",
            schema: "actions",
            table: "actions",
            type: "datetimeoffset",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DueReminderSentAt",
            schema: "actions",
            table: "actions");

        migrationBuilder.DropColumn(
            name: "EscalatedToChairmanAt",
            schema: "actions",
            table: "actions");

        migrationBuilder.DropColumn(
            name: "EscalatedToSecretaryAt",
            schema: "actions",
            table: "actions");

        migrationBuilder.DropColumn(
            name: "OverdueNotifiedAt",
            schema: "actions",
            table: "actions");
    }
}
