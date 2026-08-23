using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Research.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Research_OwnedChildAudit : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "CreatedAt",
            schema: "research",
            table: "research_recommendations",
            type: "datetimeoffset",
            nullable: false,
            defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

        migrationBuilder.AddColumn<string>(
            name: "CreatedBy",
            schema: "research",
            table: "research_recommendations",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "UpdatedAt",
            schema: "research",
            table: "research_recommendations",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "UpdatedBy",
            schema: "research",
            table: "research_recommendations",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "CreatedAt",
            schema: "research",
            table: "research_findings",
            type: "datetimeoffset",
            nullable: false,
            defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

        migrationBuilder.AddColumn<string>(
            name: "CreatedBy",
            schema: "research",
            table: "research_findings",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "UpdatedAt",
            schema: "research",
            table: "research_findings",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "UpdatedBy",
            schema: "research",
            table: "research_findings",
            type: "nvarchar(max)",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CreatedAt",
            schema: "research",
            table: "research_recommendations");

        migrationBuilder.DropColumn(
            name: "CreatedBy",
            schema: "research",
            table: "research_recommendations");

        migrationBuilder.DropColumn(
            name: "UpdatedAt",
            schema: "research",
            table: "research_recommendations");

        migrationBuilder.DropColumn(
            name: "UpdatedBy",
            schema: "research",
            table: "research_recommendations");

        migrationBuilder.DropColumn(
            name: "CreatedAt",
            schema: "research",
            table: "research_findings");

        migrationBuilder.DropColumn(
            name: "CreatedBy",
            schema: "research",
            table: "research_findings");

        migrationBuilder.DropColumn(
            name: "UpdatedAt",
            schema: "research",
            table: "research_findings");

        migrationBuilder.DropColumn(
            name: "UpdatedBy",
            schema: "research",
            table: "research_findings");
    }
}
