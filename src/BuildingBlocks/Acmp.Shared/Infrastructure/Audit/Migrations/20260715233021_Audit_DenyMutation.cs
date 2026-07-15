using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Acmp.Shared.Infrastructure.Audit.Migrations;

/// <inheritdoc />
// D-16 / C-AUDIT-04 (ADR-0031) — database-enforced append-only for the audit schema. Grants the least-priv
// application role INSERT + SELECT but DENYs UPDATE + DELETE on schema::audit, so the audit log cannot be
// altered or deleted even by a direct DBA/out-of-app write from that principal (app-layer immutability is
// already enforced by AuditEvent having no mutators/delete path). Idempotent.
//
// OPERATOR/P18 CAVEAT: this control is only EFFECTIVE once the runtime app connects as a login mapped to the
// acmp_app role. While the app connects as `sa` (db_owner/sysadmin bypasses DENY) it is inert — see ADR-0031
// and the deferred-work register. The migration ships now; making the app least-priv is P18/operator work.
public partial class Audit_DenyMutation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF DATABASE_PRINCIPAL_ID('acmp_app') IS NULL CREATE ROLE acmp_app;");
        migrationBuilder.Sql("GRANT SELECT, INSERT ON SCHEMA::audit TO acmp_app;");
        migrationBuilder.Sql("DENY UPDATE, DELETE ON SCHEMA::audit TO acmp_app;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Clear both the GRANT and the DENY (REVOKE removes either). Leave the acmp_app role in place — it
        // may hold other grants / have members; dropping a role with members would fail.
        migrationBuilder.Sql(
            "IF DATABASE_PRINCIPAL_ID('acmp_app') IS NOT NULL " +
            "REVOKE SELECT, INSERT, UPDATE, DELETE ON SCHEMA::audit FROM acmp_app;");
    }
}
