using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Integrations.Webex.Oauth.Migrations;

/// <inheritdoc />
public partial class Webex_TokenStore : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "webex");

        migrationBuilder.CreateTable(
            name: "oauth_tokens",
            schema: "webex",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false),
                AccessTokenCipher = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                RefreshTokenCipher = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                AccessTokenExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_oauth_tokens", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "oauth_tokens",
            schema: "webex");
    }
}
