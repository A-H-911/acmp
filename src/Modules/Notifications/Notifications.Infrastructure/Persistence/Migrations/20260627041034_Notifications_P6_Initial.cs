using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Notifications.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Notifications_P6_Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "notifications");

        migrationBuilder.CreateTable(
            name: "notifications",
            schema: "notifications",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RecipientUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                title_en = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                title_ar = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                body_en = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                body_ar = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                Category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                DeepLink = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                IsRead = table.Column<bool>(type: "bit", nullable: false),
                ReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_notifications", x => x.Id);
                table.UniqueConstraint("AK_notifications_PublicId", x => x.PublicId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_notifications_RecipientUserId_IsRead",
            schema: "notifications",
            table: "notifications",
            columns: new[] { "RecipientUserId", "IsRead" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "notifications",
            schema: "notifications");
    }
}
