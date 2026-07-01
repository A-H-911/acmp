using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Meetings.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Meetings_Minutes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "minutes_of_meeting",
            schema: "meetings",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                Key = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Version = table.Column<int>(type: "int", nullable: false),
                MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MeetingKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                MeetingTitle = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                summary_en = table.Column<string>(type: "nvarchar(max)", nullable: false),
                summary_ar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ApprovedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                ApprovedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ApprovedBySoleAuthor = table.Column<bool>(type: "bit", nullable: false),
                PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                SupersededByMinutesId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                supersession_reason_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                supersession_reason_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_minutes_of_meeting", x => x.Id);
                table.UniqueConstraint("AK_minutes_of_meeting_PublicId", x => x.PublicId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_minutes_of_meeting_Key_Version",
            schema: "meetings",
            table: "minutes_of_meeting",
            columns: new[] { "Key", "Version" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_minutes_of_meeting_MeetingId",
            schema: "meetings",
            table: "minutes_of_meeting",
            column: "MeetingId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "minutes_of_meeting",
            schema: "meetings");
    }
}
