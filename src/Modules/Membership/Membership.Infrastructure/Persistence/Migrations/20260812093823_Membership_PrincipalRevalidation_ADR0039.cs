using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Membership.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Membership_PrincipalRevalidation_ADR0039 : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "AccessExpiresAt",
            schema: "membership",
            table: "committee_members",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "RolesChangedAt",
            schema: "membership",
            table: "committee_members",
            type: "datetimeoffset",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AccessExpiresAt",
            schema: "membership",
            table: "committee_members");

        migrationBuilder.DropColumn(
            name: "RolesChangedAt",
            schema: "membership",
            table: "committee_members");
    }
}
