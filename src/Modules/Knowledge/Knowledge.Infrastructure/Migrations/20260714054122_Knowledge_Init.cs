using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Knowledge.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Knowledge_Init : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "knowledge");

        migrationBuilder.CreateTable(
            name: "documents",
            schema: "knowledge",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                Key = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                title_en = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                title_ar = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                body_en = table.Column<string>(type: "nvarchar(max)", nullable: false),
                body_ar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Category = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                OwnerUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Version = table.Column<int>(type: "int", nullable: false),
                tags = table.Column<string>(type: "nvarchar(max)", nullable: false),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_documents", x => x.Id);
                table.UniqueConstraint("AK_documents_PublicId", x => x.PublicId);
            });

        migrationBuilder.CreateTable(
            name: "knowledge_key_counters",
            schema: "knowledge",
            columns: table => new
            {
                Prefix = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                Year = table.Column<int>(type: "int", nullable: false),
                Next = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_knowledge_key_counters", x => new { x.Prefix, x.Year });
            });

        migrationBuilder.CreateTable(
            name: "knowledge_document_versions",
            schema: "knowledge",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Version = table.Column<int>(type: "int", nullable: false),
                title_en = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                title_ar = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                body_en = table.Column<string>(type: "nvarchar(max)", nullable: false),
                body_ar = table.Column<string>(type: "nvarchar(max)", nullable: false),
                SavedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                SavedByUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                DocumentEntityId = table.Column<long>(type: "bigint", nullable: false),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_knowledge_document_versions", x => x.Id);
                table.ForeignKey(
                    name: "FK_knowledge_document_versions_documents_DocumentEntityId",
                    column: x => x.DocumentEntityId,
                    principalSchema: "knowledge",
                    principalTable: "documents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_documents_Key",
            schema: "knowledge",
            table: "documents",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_documents_Status",
            schema: "knowledge",
            table: "documents",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_knowledge_document_versions_DocumentEntityId",
            schema: "knowledge",
            table: "knowledge_document_versions",
            column: "DocumentEntityId");

        migrationBuilder.CreateIndex(
            name: "IX_knowledge_document_versions_PublicId",
            schema: "knowledge",
            table: "knowledge_document_versions",
            column: "PublicId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "knowledge_document_versions",
            schema: "knowledge");

        migrationBuilder.DropTable(
            name: "knowledge_key_counters",
            schema: "knowledge");

        migrationBuilder.DropTable(
            name: "documents",
            schema: "knowledge");
    }
}
