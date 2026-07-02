using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Shared.Infrastructure.Audit.Migrations;

/// <inheritdoc />
public partial class Audit_Init : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "audit");

        migrationBuilder.CreateTable(
            name: "AuditEvents",
            schema: "audit",
            columns: table => new
            {
                Sequence = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                EventType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                DataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                PreviousHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditEvents", x => x.Sequence);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AuditEvents_Hash",
            schema: "audit",
            table: "AuditEvents",
            column: "Hash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AuditEvents_PreviousHash",
            schema: "audit",
            table: "AuditEvents",
            column: "PreviousHash",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AuditEvents",
            schema: "audit");
    }
}
