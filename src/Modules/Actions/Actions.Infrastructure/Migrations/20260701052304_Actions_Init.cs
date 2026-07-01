using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Actions.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Actions_Init : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "actions");

        migrationBuilder.CreateTable(
            name: "action_key_counters",
            schema: "actions",
            columns: table => new
            {
                Prefix = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                Year = table.Column<int>(type: "int", nullable: false),
                Next = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_action_key_counters", x => new { x.Prefix, x.Year });
            });

        migrationBuilder.CreateTable(
            name: "actions",
            schema: "actions",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                Key = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                title_en = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                title_ar = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                description_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                description_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                Priority = table.Column<int>(type: "int", nullable: false),
                OwnerUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                OwnerName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                DueDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ProgressPct = table.Column<int>(type: "int", nullable: false),
                SourceType = table.Column<int>(type: "int", nullable: false),
                SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                MeetingKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                blocked_reason_en = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                blocked_reason_ar = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                completion_note_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                completion_note_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                cancel_reason_en = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                cancel_reason_ar = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                CompletedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                VerifiedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                VerifiedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                VerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_actions", x => x.Id);
                table.UniqueConstraint("AK_actions_PublicId", x => x.PublicId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_actions_Key",
            schema: "actions",
            table: "actions",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_actions_OwnerUserId",
            schema: "actions",
            table: "actions",
            column: "OwnerUserId");

        migrationBuilder.CreateIndex(
            name: "IX_actions_SourceType_SourceId",
            schema: "actions",
            table: "actions",
            columns: new[] { "SourceType", "SourceId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "action_key_counters",
            schema: "actions");

        migrationBuilder.DropTable(
            name: "actions",
            schema: "actions");
    }
}
