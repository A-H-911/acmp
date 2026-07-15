using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Decisions.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Decisions_BallotChain : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ChainSealedAt",
            schema: "decisions",
            table: "votes",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Hash",
            schema: "decisions",
            table: "vote_ballots",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PreviousHash",
            schema: "decisions",
            table: "vote_ballots",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ChainSealedAt",
            schema: "decisions",
            table: "votes");

        migrationBuilder.DropColumn(
            name: "Hash",
            schema: "decisions",
            table: "vote_ballots");

        migrationBuilder.DropColumn(
            name: "PreviousHash",
            schema: "decisions",
            table: "vote_ballots");
    }
}
