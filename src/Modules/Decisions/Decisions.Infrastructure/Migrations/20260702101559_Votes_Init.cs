using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Decisions.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Votes_Init : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "votes",
            schema: "decisions",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                Key = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                TopicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                options_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                AllowAbstain = table.Column<bool>(type: "bit", nullable: false),
                quorum_min_present = table.Column<int>(type: "int", nullable: false),
                quorum_min_cast = table.Column<int>(type: "int", nullable: false),
                tally_json = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                ResultSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                OpenedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CounterUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                CounterName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_votes", x => x.Id);
                table.UniqueConstraint("AK_votes_PublicId", x => x.PublicId);
            });

        migrationBuilder.CreateTable(
            name: "vote_ballots",
            schema: "decisions",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                VoterUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                VoterName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Choice = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                comment_en = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                comment_ar = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                Recused = table.Column<bool>(type: "bit", nullable: false),
                CastAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                VoteEntityId = table.Column<long>(type: "bigint", nullable: false),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_vote_ballots", x => x.Id);
                table.ForeignKey(
                    name: "FK_vote_ballots_votes_VoteEntityId",
                    column: x => x.VoteEntityId,
                    principalSchema: "decisions",
                    principalTable: "votes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_vote_ballots_PublicId",
            schema: "decisions",
            table: "vote_ballots",
            column: "PublicId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_vote_ballots_VoteEntityId_VoterUserId",
            schema: "decisions",
            table: "vote_ballots",
            columns: new[] { "VoteEntityId", "VoterUserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_votes_Key",
            schema: "decisions",
            table: "votes",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_votes_TopicId",
            schema: "decisions",
            table: "votes",
            column: "TopicId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "vote_ballots",
            schema: "decisions");

        migrationBuilder.DropTable(
            name: "votes",
            schema: "decisions");
    }
}
