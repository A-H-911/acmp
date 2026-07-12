using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Research.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Research_Init : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "research");

        migrationBuilder.CreateTable(
            name: "research_key_counters",
            schema: "research",
            columns: table => new
            {
                Prefix = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                Year = table.Column<int>(type: "int", nullable: false),
                Next = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_research_key_counters", x => new { x.Prefix, x.Year });
            });

        migrationBuilder.CreateTable(
            name: "research_missions",
            schema: "research",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                Key = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                title_en = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                title_ar = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                question_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                question_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                OwnerUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                OwnerName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                KeystonePackageRef = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                SourceTopicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                cancellation_reason_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                cancellation_reason_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_research_missions", x => x.Id);
                table.UniqueConstraint("AK_research_missions_PublicId", x => x.PublicId);
            });

        migrationBuilder.CreateTable(
            name: "research_findings",
            schema: "research",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Key = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                summary_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                summary_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                detail_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                detail_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                Confidence = table.Column<int>(type: "int", nullable: false),
                IsVerified = table.Column<bool>(type: "bit", nullable: false),
                MissionEntityId = table.Column<long>(type: "bigint", nullable: false),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_research_findings", x => x.Id);
                table.ForeignKey(
                    name: "FK_research_findings_research_missions_MissionEntityId",
                    column: x => x.MissionEntityId,
                    principalSchema: "research",
                    principalTable: "research_missions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "research_recommendations",
            schema: "research",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Key = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                statement_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                statement_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                rationale_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                rationale_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                Priority = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                LinkedTopicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                MissionEntityId = table.Column<long>(type: "bigint", nullable: false),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_research_recommendations", x => x.Id);
                table.ForeignKey(
                    name: "FK_research_recommendations_research_missions_MissionEntityId",
                    column: x => x.MissionEntityId,
                    principalSchema: "research",
                    principalTable: "research_missions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_research_findings_MissionEntityId",
            schema: "research",
            table: "research_findings",
            column: "MissionEntityId");

        migrationBuilder.CreateIndex(
            name: "IX_research_findings_PublicId",
            schema: "research",
            table: "research_findings",
            column: "PublicId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_research_missions_Key",
            schema: "research",
            table: "research_missions",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_research_missions_Status",
            schema: "research",
            table: "research_missions",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_research_recommendations_MissionEntityId",
            schema: "research",
            table: "research_recommendations",
            column: "MissionEntityId");

        migrationBuilder.CreateIndex(
            name: "IX_research_recommendations_PublicId",
            schema: "research",
            table: "research_recommendations",
            column: "PublicId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "research_findings",
            schema: "research");

        migrationBuilder.DropTable(
            name: "research_key_counters",
            schema: "research");

        migrationBuilder.DropTable(
            name: "research_recommendations",
            schema: "research");

        migrationBuilder.DropTable(
            name: "research_missions",
            schema: "research");
    }
}
