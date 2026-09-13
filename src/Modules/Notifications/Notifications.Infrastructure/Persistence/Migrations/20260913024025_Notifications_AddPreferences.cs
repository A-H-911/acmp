using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Notifications.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Notifications_AddPreferences : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "notification_preferences",
            schema: "notifications",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Channel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_notification_preferences", x => x.Id);
                table.UniqueConstraint("AK_notification_preferences_PublicId", x => x.PublicId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_notification_preferences_UserId_Category_Channel",
            schema: "notifications",
            table: "notification_preferences",
            columns: new[] { "UserId", "Category", "Channel" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "notification_preferences",
            schema: "notifications");
    }
}
