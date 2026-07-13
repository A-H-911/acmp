using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Topics.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Topics_ConvertProvenance : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "SourceRecommendationId",
            schema: "topics",
            table: "topics",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_topics_SourceRecommendationId",
            schema: "topics",
            table: "topics",
            column: "SourceRecommendationId",
            unique: true,
            filter: "[SourceRecommendationId] IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_topics_SourceRecommendationId",
            schema: "topics",
            table: "topics");

        migrationBuilder.DropColumn(
            name: "SourceRecommendationId",
            schema: "topics",
            table: "topics");
    }
}
