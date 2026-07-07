using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Meetings.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Meetings_AddWebexFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RecordingDownloadUrl",
            schema: "meetings",
            table: "meetings",
            type: "nvarchar(2048)",
            maxLength: 2048,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "RecordingDurationSeconds",
            schema: "meetings",
            table: "meetings",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RecordingUrl",
            schema: "meetings",
            table: "meetings",
            type: "nvarchar(2048)",
            maxLength: 2048,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "WebexMeetingId",
            schema: "meetings",
            table: "meetings",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_meetings_WebexMeetingId",
            schema: "meetings",
            table: "meetings",
            column: "WebexMeetingId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_meetings_WebexMeetingId",
            schema: "meetings",
            table: "meetings");

        migrationBuilder.DropColumn(
            name: "RecordingDownloadUrl",
            schema: "meetings",
            table: "meetings");

        migrationBuilder.DropColumn(
            name: "RecordingDurationSeconds",
            schema: "meetings",
            table: "meetings");

        migrationBuilder.DropColumn(
            name: "RecordingUrl",
            schema: "meetings",
            table: "meetings");

        migrationBuilder.DropColumn(
            name: "WebexMeetingId",
            schema: "meetings",
            table: "meetings");
    }
}
