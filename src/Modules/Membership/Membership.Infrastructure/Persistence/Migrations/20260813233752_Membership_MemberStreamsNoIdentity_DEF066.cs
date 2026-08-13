using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Modules.Membership.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
// DEF-066 — membership.member_streams.StreamId shipped as an IDENTITY column, so every assignment,
// which sets a real membership.streams.Id, was refused by SQL Server ("Cannot insert explicit value
// for identity column"). That made PUT /api/members/{publicId}/streams and the FR-156 invite
// non-functional against any real database, while passing everywhere because Acmp.Api.Tests runs on
// the InMemory provider, which has no identity columns. It also blocks the ADR-0043 step-5 backfill,
// which is why this migration is ordered immediately before it.
//
// ⚠ WRITTEN BY HAND BECAUSE THE GENERATED AlterColumn CANNOT WORK: EF scaffolds the model change as
// an AlterColumn dropping the annotation, and applying it throws "To change the IDENTITY property of
// a column, the column needs to be dropped and recreated." SQL Server has no ALTER for it. The table
// is therefore rebuilt — the same shape EF itself emits for a SQLite column rebuild.
//
// ⚠ ROWS ARE COPIED RATHER THAN DROPPED even though this table should be empty in every environment
// (no assignment has ever succeeded, so only a hand-inserted row could exist). "Should be empty" is
// a belief about a deployed database, and this project has been burned by acting on one; copying
// makes the belief irrelevant instead of checked.
public partial class Membership_MemberStreamsNoIdentity_DEF066 : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE membership.member_streams_rebuild (
            CommitteeMemberId bigint NOT NULL,
            StreamId bigint NOT NULL,
            CONSTRAINT PK_member_streams_rebuild PRIMARY KEY (CommitteeMemberId, StreamId),
            CONSTRAINT FK_member_streams_rebuild_committee_members_CommitteeMemberId
                FOREIGN KEY (CommitteeMemberId) REFERENCES membership.committee_members (Id) ON DELETE CASCADE
        );

        INSERT INTO membership.member_streams_rebuild (CommitteeMemberId, StreamId)
        SELECT CommitteeMemberId, StreamId FROM membership.member_streams;

        DROP TABLE membership.member_streams;
        EXEC sp_rename N'membership.member_streams_rebuild', N'member_streams';
        EXEC sp_rename N'membership.PK_member_streams_rebuild', N'PK_member_streams', N'OBJECT';
        EXEC sp_rename N'membership.FK_member_streams_rebuild_committee_members_CommitteeMemberId',
                       N'FK_member_streams_committee_members_CommitteeMemberId', N'OBJECT';
        """);

    /// <inheritdoc />
    // The same rebuild in reverse, restoring the identity. Rows are copied under IDENTITY_INSERT so
    // a rollback does not renumber assignments into pointing at different streams.
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE TABLE membership.member_streams_rebuild (
            CommitteeMemberId bigint NOT NULL,
            StreamId bigint NOT NULL IDENTITY(1, 1),
            CONSTRAINT PK_member_streams_rebuild PRIMARY KEY (CommitteeMemberId, StreamId),
            CONSTRAINT FK_member_streams_rebuild_committee_members_CommitteeMemberId
                FOREIGN KEY (CommitteeMemberId) REFERENCES membership.committee_members (Id) ON DELETE CASCADE
        );

        SET IDENTITY_INSERT membership.member_streams_rebuild ON;
        INSERT INTO membership.member_streams_rebuild (CommitteeMemberId, StreamId)
        SELECT CommitteeMemberId, StreamId FROM membership.member_streams;
        SET IDENTITY_INSERT membership.member_streams_rebuild OFF;

        DROP TABLE membership.member_streams;
        EXEC sp_rename N'membership.member_streams_rebuild', N'member_streams';
        EXEC sp_rename N'membership.PK_member_streams_rebuild', N'PK_member_streams', N'OBJECT';
        EXEC sp_rename N'membership.FK_member_streams_rebuild_committee_members_CommitteeMemberId',
                       N'FK_member_streams_committee_members_CommitteeMemberId', N'OBJECT';
        """);
}
