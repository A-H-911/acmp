using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Risks.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Risks_Init : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "risks");

        migrationBuilder.CreateTable(
            name: "risk_key_counters",
            schema: "risks",
            columns: table => new
            {
                Prefix = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                Year = table.Column<int>(type: "int", nullable: false),
                Next = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_risk_key_counters", x => new { x.Prefix, x.Year });
            });

        migrationBuilder.CreateTable(
            name: "risks",
            schema: "risks",
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
                Likelihood = table.Column<int>(type: "int", nullable: false),
                Impact = table.Column<int>(type: "int", nullable: false),
                OwnerUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                OwnerName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                SubjectType = table.Column<int>(type: "int", nullable: false),
                SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SubjectKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                closure_note_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                closure_note_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                acceptance_rationale_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                acceptance_rationale_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                AcceptingAuthority = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                escalation_reason_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                escalation_reason_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                EscalationTarget = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_risks", x => x.Id);
                table.UniqueConstraint("AK_risks_PublicId", x => x.PublicId);
            });

        migrationBuilder.CreateTable(
            name: "risk_mitigations",
            schema: "risks",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                description_en = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                description_ar = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                Type = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                OwnerUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                LinkedActionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                DueDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                RiskEntityId = table.Column<long>(type: "bigint", nullable: false),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_risk_mitigations", x => x.Id);
                table.ForeignKey(
                    name: "FK_risk_mitigations_risks_RiskEntityId",
                    column: x => x.RiskEntityId,
                    principalSchema: "risks",
                    principalTable: "risks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_risk_mitigations_PublicId",
            schema: "risks",
            table: "risk_mitigations",
            column: "PublicId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_risk_mitigations_RiskEntityId",
            schema: "risks",
            table: "risk_mitigations",
            column: "RiskEntityId");

        migrationBuilder.CreateIndex(
            name: "IX_risks_Key",
            schema: "risks",
            table: "risks",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_risks_OwnerUserId",
            schema: "risks",
            table: "risks",
            column: "OwnerUserId");

        migrationBuilder.CreateIndex(
            name: "IX_risks_SubjectType_SubjectId",
            schema: "risks",
            table: "risks",
            columns: new[] { "SubjectType", "SubjectId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "risk_key_counters",
            schema: "risks");

        migrationBuilder.DropTable(
            name: "risk_mitigations",
            schema: "risks");

        migrationBuilder.DropTable(
            name: "risks",
            schema: "risks");
    }
}
