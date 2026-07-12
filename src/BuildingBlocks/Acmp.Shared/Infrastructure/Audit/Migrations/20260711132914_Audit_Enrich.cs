using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Shared.Infrastructure.Audit.Migrations;

/// <inheritdoc />
public partial class Audit_Enrich : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Action",
            schema: "audit",
            table: "AuditEvents",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ActorRole",
            schema: "audit",
            table: "AuditEvents",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ActorUserId",
            schema: "audit",
            table: "AuditEvents",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AfterJson",
            schema: "audit",
            table: "AuditEvents",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "BeforeJson",
            schema: "audit",
            table: "AuditEvents",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CorrelationId",
            schema: "audit",
            table: "AuditEvents",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "HashVersion",
            schema: "audit",
            table: "AuditEvents",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<string>(
            name: "Outcome",
            schema: "audit",
            table: "AuditEvents",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SubjectId",
            schema: "audit",
            table: "AuditEvents",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SubjectType",
            schema: "audit",
            table: "AuditEvents",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Action",
            schema: "audit",
            table: "AuditEvents");

        migrationBuilder.DropColumn(
            name: "ActorRole",
            schema: "audit",
            table: "AuditEvents");

        migrationBuilder.DropColumn(
            name: "ActorUserId",
            schema: "audit",
            table: "AuditEvents");

        migrationBuilder.DropColumn(
            name: "AfterJson",
            schema: "audit",
            table: "AuditEvents");

        migrationBuilder.DropColumn(
            name: "BeforeJson",
            schema: "audit",
            table: "AuditEvents");

        migrationBuilder.DropColumn(
            name: "CorrelationId",
            schema: "audit",
            table: "AuditEvents");

        migrationBuilder.DropColumn(
            name: "HashVersion",
            schema: "audit",
            table: "AuditEvents");

        migrationBuilder.DropColumn(
            name: "Outcome",
            schema: "audit",
            table: "AuditEvents");

        migrationBuilder.DropColumn(
            name: "SubjectId",
            schema: "audit",
            table: "AuditEvents");

        migrationBuilder.DropColumn(
            name: "SubjectType",
            schema: "audit",
            table: "AuditEvents");
    }
}
