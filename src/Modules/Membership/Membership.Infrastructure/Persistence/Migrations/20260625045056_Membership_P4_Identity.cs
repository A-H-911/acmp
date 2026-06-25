using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Membership.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Membership_P4_Identity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Explicit drop + add (not a rename): IsActive is superseded by MembershipStatus, so its
        // boolean values must NOT carry into the new, unrelated IsVotingEligible flag.
        migrationBuilder.DropColumn(
            name: "IsActive",
            schema: "membership",
            table: "committee_members");

        migrationBuilder.AddColumn<bool>(
            name: "IsVotingEligible",
            schema: "membership",
            table: "committee_members",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "Status",
            schema: "membership",
            table: "committee_members",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "delegations",
            schema: "membership",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                DelegatorMemberId = table.Column<long>(type: "bigint", nullable: false),
                DelegateMemberId = table.Column<long>(type: "bigint", nullable: false),
                Capability = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                ValidFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ValidTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_delegations", x => x.Id);
                table.UniqueConstraint("AK_delegations_PublicId", x => x.PublicId);
                table.ForeignKey(
                    name: "FK_delegations_committee_members_DelegateMemberId",
                    column: x => x.DelegateMemberId,
                    principalSchema: "membership",
                    principalTable: "committee_members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_delegations_committee_members_DelegatorMemberId",
                    column: x => x.DelegatorMemberId,
                    principalSchema: "membership",
                    principalTable: "committee_members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "member_streams",
            schema: "membership",
            columns: table => new
            {
                CommitteeMemberId = table.Column<long>(type: "bigint", nullable: false),
                StreamId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_member_streams", x => new { x.CommitteeMemberId, x.StreamId });
                table.ForeignKey(
                    name: "FK_member_streams_committee_members_CommitteeMemberId",
                    column: x => x.CommitteeMemberId,
                    principalSchema: "membership",
                    principalTable: "committee_members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "streams",
            schema: "membership",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                name_en = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                name_ar = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_streams", x => x.Id);
                table.UniqueConstraint("AK_streams_PublicId", x => x.PublicId);
            });

        migrationBuilder.CreateTable(
            name: "topic_capability_grants",
            schema: "membership",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                CommitteeMemberId = table.Column<long>(type: "bigint", nullable: false),
                TopicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Capability = table.Column<int>(type: "int", nullable: false),
                MeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ValidFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ValidTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_topic_capability_grants", x => x.Id);
                table.UniqueConstraint("AK_topic_capability_grants_PublicId", x => x.PublicId);
                table.ForeignKey(
                    name: "FK_topic_capability_grants_committee_members_CommitteeMemberId",
                    column: x => x.CommitteeMemberId,
                    principalSchema: "membership",
                    principalTable: "committee_members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_delegations_DelegateMemberId_ValidTo",
            schema: "membership",
            table: "delegations",
            columns: new[] { "DelegateMemberId", "ValidTo" });

        migrationBuilder.CreateIndex(
            name: "IX_delegations_DelegatorMemberId",
            schema: "membership",
            table: "delegations",
            column: "DelegatorMemberId");

        migrationBuilder.CreateIndex(
            name: "IX_streams_Code",
            schema: "membership",
            table: "streams",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_topic_capability_grants_CommitteeMemberId_TopicId",
            schema: "membership",
            table: "topic_capability_grants",
            columns: new[] { "CommitteeMemberId", "TopicId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "delegations",
            schema: "membership");

        migrationBuilder.DropTable(
            name: "member_streams",
            schema: "membership");

        migrationBuilder.DropTable(
            name: "streams",
            schema: "membership");

        migrationBuilder.DropTable(
            name: "topic_capability_grants",
            schema: "membership");

        migrationBuilder.DropColumn(
            name: "Status",
            schema: "membership",
            table: "committee_members");

        migrationBuilder.DropColumn(
            name: "IsVotingEligible",
            schema: "membership",
            table: "committee_members");

        migrationBuilder.AddColumn<bool>(
            name: "IsActive",
            schema: "membership",
            table: "committee_members",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }
}
