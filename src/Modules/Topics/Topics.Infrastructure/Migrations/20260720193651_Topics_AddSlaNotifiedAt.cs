using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Topics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Topics_AddSlaNotifiedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SlaNotifiedAt",
                schema: "topics",
                table: "topics",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SlaNotifiedAt",
                schema: "topics",
                table: "topics");
        }
    }
}
