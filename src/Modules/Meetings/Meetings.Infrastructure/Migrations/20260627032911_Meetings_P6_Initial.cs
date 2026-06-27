using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Meetings.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Meetings_P6_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "meetings");

            migrationBuilder.CreateTable(
                name: "agendas",
                schema: "meetings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agendas", x => x.Id);
                    table.UniqueConstraint("AK_agendas_PublicId", x => x.PublicId);
                });

            migrationBuilder.CreateTable(
                name: "meeting_key_counters",
                schema: "meetings",
                columns: table => new
                {
                    Prefix = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Next = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meeting_key_counters", x => new { x.Prefix, x.Year });
                });

            migrationBuilder.CreateTable(
                name: "meetings",
                schema: "meetings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CommitteeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduledStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ScheduledEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    JoinUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ChairUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChairName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    HeldAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meetings", x => x.Id);
                    table.UniqueConstraint("AK_meetings_PublicId", x => x.PublicId);
                });

            migrationBuilder.CreateTable(
                name: "agenda_items",
                schema: "meetings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TopicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TopicKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TopicTitle = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Urgent = table.Column<bool>(type: "bit", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    TimeboxMinutes = table.Column<int>(type: "int", nullable: false),
                    PresenterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PresenterName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    ActualMinutes = table.Column<int>(type: "int", nullable: false),
                    CarryOverFromAgendaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AgendaEntityId = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agenda_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agenda_items_agendas_AgendaEntityId",
                        column: x => x.AgendaEntityId,
                        principalSchema: "meetings",
                        principalTable: "agendas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meeting_attendance",
                schema: "meetings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsVotingEligible = table.Column<bool>(type: "bit", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LeftAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    MeetingEntityId = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meeting_attendance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_meeting_attendance_meetings_MeetingEntityId",
                        column: x => x.MeetingEntityId,
                        principalSchema: "meetings",
                        principalTable: "meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meeting_discussions",
                schema: "meetings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TopicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthorSub = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AuthorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Origin = table.Column<int>(type: "int", nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    MeetingEntityId = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meeting_discussions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_meeting_discussions_meetings_MeetingEntityId",
                        column: x => x.MeetingEntityId,
                        principalSchema: "meetings",
                        principalTable: "meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agenda_items_AgendaEntityId_TopicId",
                schema: "meetings",
                table: "agenda_items",
                columns: new[] { "AgendaEntityId", "TopicId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agenda_items_PublicId",
                schema: "meetings",
                table: "agenda_items",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agendas_Key",
                schema: "meetings",
                table: "agendas",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agendas_MeetingId",
                schema: "meetings",
                table: "agendas",
                column: "MeetingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_meeting_attendance_MeetingEntityId_UserId",
                schema: "meetings",
                table: "meeting_attendance",
                columns: new[] { "MeetingEntityId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_meeting_attendance_PublicId",
                schema: "meetings",
                table: "meeting_attendance",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_meeting_discussions_MeetingEntityId",
                schema: "meetings",
                table: "meeting_discussions",
                column: "MeetingEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_meeting_discussions_PublicId",
                schema: "meetings",
                table: "meeting_discussions",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_meetings_Key",
                schema: "meetings",
                table: "meetings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_meetings_ScheduledStart",
                schema: "meetings",
                table: "meetings",
                column: "ScheduledStart");

            migrationBuilder.CreateIndex(
                name: "IX_meetings_Status",
                schema: "meetings",
                table: "meetings",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agenda_items",
                schema: "meetings");

            migrationBuilder.DropTable(
                name: "meeting_attendance",
                schema: "meetings");

            migrationBuilder.DropTable(
                name: "meeting_discussions",
                schema: "meetings");

            migrationBuilder.DropTable(
                name: "meeting_key_counters",
                schema: "meetings");

            migrationBuilder.DropTable(
                name: "agendas",
                schema: "meetings");

            migrationBuilder.DropTable(
                name: "meetings",
                schema: "meetings");
        }
    }
}
