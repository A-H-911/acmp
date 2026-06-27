using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Membership.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Membership_FilteredEmailUniqueIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_committee_members_Email",
            schema: "membership",
            table: "committee_members");

        migrationBuilder.CreateIndex(
            name: "IX_committee_members_Email",
            schema: "membership",
            table: "committee_members",
            column: "Email",
            unique: true,
            filter: "[Email] <> ''");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_committee_members_Email",
            schema: "membership",
            table: "committee_members");

        migrationBuilder.CreateIndex(
            name: "IX_committee_members_Email",
            schema: "membership",
            table: "committee_members",
            column: "Email",
            unique: true);
    }
}
