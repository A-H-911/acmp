using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Decisions.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Decisions_Init : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "decisions");

        migrationBuilder.CreateTable(
            name: "decision_key_counters",
            schema: "decisions",
            columns: table => new
            {
                Prefix = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                Year = table.Column<int>(type: "int", nullable: false),
                Next = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_decision_key_counters", x => new { x.Prefix, x.Year });
            });

        migrationBuilder.CreateTable(
            name: "decisions",
            schema: "decisions",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                Key = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                TopicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Outcome = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                rationale_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                rationale_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                alternatives_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                alternatives_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                VoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ChairApprovedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                ChairApprovedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                ChairOverride = table.Column<bool>(type: "bit", nullable: false),
                override_justification_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                override_justification_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                SupersededByDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                table.PrimaryKey("PK_decisions", x => x.Id);
                table.UniqueConstraint("AK_decisions_PublicId", x => x.PublicId);
            });

        migrationBuilder.CreateTable(
            name: "decision_conditions",
            schema: "decisions",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                text_en = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                text_ar = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                DueDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LinkedActionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                DecisionEntityId = table.Column<long>(type: "bigint", nullable: false),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_decision_conditions", x => x.Id);
                table.ForeignKey(
                    name: "FK_decision_conditions_decisions_DecisionEntityId",
                    column: x => x.DecisionEntityId,
                    principalSchema: "decisions",
                    principalTable: "decisions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_decision_conditions_DecisionEntityId",
            schema: "decisions",
            table: "decision_conditions",
            column: "DecisionEntityId");

        migrationBuilder.CreateIndex(
            name: "IX_decision_conditions_PublicId",
            schema: "decisions",
            table: "decision_conditions",
            column: "PublicId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_decisions_Key",
            schema: "decisions",
            table: "decisions",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_decisions_TopicId",
            schema: "decisions",
            table: "decisions",
            column: "TopicId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "decision_conditions",
            schema: "decisions");

        migrationBuilder.DropTable(
            name: "decision_key_counters",
            schema: "decisions");

        migrationBuilder.DropTable(
            name: "decisions",
            schema: "decisions");
    }
}
