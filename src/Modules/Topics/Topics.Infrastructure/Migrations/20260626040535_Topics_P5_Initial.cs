using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Topics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Topics_P5_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "topics");

            migrationBuilder.CreateTable(
                name: "topic_key_counters",
                schema: "topics",
                columns: table => new
                {
                    Year = table.Column<int>(type: "int", nullable: false),
                    Next = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topic_key_counters", x => x.Year);
                });

            migrationBuilder.CreateTable(
                name: "topics",
                schema: "topics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: ""),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Urgency = table.Column<int>(type: "int", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    SubmittedBySub = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SubmittedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RevisitOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    streams = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    systems = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    tags = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topics", x => x.Id);
                    table.UniqueConstraint("AK_topics_PublicId", x => x.PublicId);
                });

            migrationBuilder.CreateTable(
                name: "topic_attachments",
                schema: "topics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    UploadedBySub = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    UploadedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TopicEntityId = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topic_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_topic_attachments_topics_TopicEntityId",
                        column: x => x.TopicEntityId,
                        principalSchema: "topics",
                        principalTable: "topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "topic_comments",
                schema: "topics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthorSub = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AuthorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TopicEntityId = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topic_comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_topic_comments_topics_TopicEntityId",
                        column: x => x.TopicEntityId,
                        principalSchema: "topics",
                        principalTable: "topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "topic_status_events",
                schema: "topics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FromStatus = table.Column<int>(type: "int", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ActorSub = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TopicEntityId = table.Column<long>(type: "bigint", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topic_status_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_topic_status_events_topics_TopicEntityId",
                        column: x => x.TopicEntityId,
                        principalSchema: "topics",
                        principalTable: "topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_topic_attachments_PublicId",
                schema: "topics",
                table: "topic_attachments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_topic_attachments_TopicEntityId",
                schema: "topics",
                table: "topic_attachments",
                column: "TopicEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_topic_comments_PublicId",
                schema: "topics",
                table: "topic_comments",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_topic_comments_TopicEntityId",
                schema: "topics",
                table: "topic_comments",
                column: "TopicEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_topic_status_events_PublicId",
                schema: "topics",
                table: "topic_status_events",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_topic_status_events_TopicEntityId",
                schema: "topics",
                table: "topic_status_events",
                column: "TopicEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_topics_Key",
                schema: "topics",
                table: "topics",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_topics_OwnerId",
                schema: "topics",
                table: "topics",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_topics_Status",
                schema: "topics",
                table: "topics",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "topic_attachments",
                schema: "topics");

            migrationBuilder.DropTable(
                name: "topic_comments",
                schema: "topics");

            migrationBuilder.DropTable(
                name: "topic_key_counters",
                schema: "topics");

            migrationBuilder.DropTable(
                name: "topic_status_events",
                schema: "topics");

            migrationBuilder.DropTable(
                name: "topics",
                schema: "topics");
        }
    }
}
