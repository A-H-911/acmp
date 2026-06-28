using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Meetings.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Meetings_AddTypeMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Mode",
                schema: "meetings",
                table: "meetings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                schema: "meetings",
                table: "meetings",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Mode",
                schema: "meetings",
                table: "meetings");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "meetings",
                table: "meetings");
        }
    }
}
