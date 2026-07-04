using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Governance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Governance_AdrSourceDecisionUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_adrs_SourceDecisionId",
                schema: "governance",
                table: "adrs",
                column: "SourceDecisionId",
                unique: true,
                filter: "[SourceDecisionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_adrs_SourceDecisionId",
                schema: "governance",
                table: "adrs");
        }
    }
}
