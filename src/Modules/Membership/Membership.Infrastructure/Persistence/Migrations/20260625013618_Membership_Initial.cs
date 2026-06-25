using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Membership.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Membership_Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "membership");

        migrationBuilder.CreateTable(
            name: "committee_members",
            schema: "membership",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                KeycloakUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                FullName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Role = table.Column<int>(type: "int", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_committee_members", x => x.Id);
                table.UniqueConstraint("AK_committee_members_PublicId", x => x.PublicId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_committee_members_Email",
            schema: "membership",
            table: "committee_members",
            column: "Email",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_committee_members_KeycloakUserId",
            schema: "membership",
            table: "committee_members",
            column: "KeycloakUserId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "committee_members",
            schema: "membership");
    }
}
