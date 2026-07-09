using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Meetings.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Meetings_AddRecordingUpload : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RecordingContentType",
            schema: "meetings",
            table: "meetings",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RecordingFileName",
            schema: "meetings",
            table: "meetings",
            type: "nvarchar(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RecordingObjectKey",
            schema: "meetings",
            table: "meetings",
            type: "nvarchar(512)",
            maxLength: 512,
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "RecordingSizeBytes",
            schema: "meetings",
            table: "meetings",
            type: "bigint",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RecordingContentType",
            schema: "meetings",
            table: "meetings");

        migrationBuilder.DropColumn(
            name: "RecordingFileName",
            schema: "meetings",
            table: "meetings");

        migrationBuilder.DropColumn(
            name: "RecordingObjectKey",
            schema: "meetings",
            table: "meetings");

        migrationBuilder.DropColumn(
            name: "RecordingSizeBytes",
            schema: "meetings",
            table: "meetings");
    }
}
