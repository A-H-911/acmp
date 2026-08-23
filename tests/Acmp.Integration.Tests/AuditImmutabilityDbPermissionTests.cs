using Acmp.Shared.Infrastructure.Audit;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Integration.Tests;

// D-16 / C-AUDIT-04 (ADR-0031) — proves the Audit_DenyMutation migration: a principal in the least-priv
// acmp_app role can APPEND and READ audit rows but the database itself refuses UPDATE/DELETE on schema::audit.
// This is the leg that makes the audit log immutable against a direct DBA/out-of-app write (not only the app
// layer). Runs on the real SQL Server container (the InMemory provider ignores permissions entirely).
//
// CAVEAT (ADR-0031): in the shipped dev/e2e stack the app connects as `sa`, which bypasses DENY — so this
// control is INERT there until an operator maps the app to a least-priv login (P18). The test provisions such
// a login (`probe`) to prove the mechanism works as designed.
[Collection(SqlBackstopCollection.Name)]
[Trait("Category", "Security")]
public class AuditImmutabilityDbPermissionTests
{
    private const string ProbePassword = "Pr0be#Passw0rd!";

    private readonly SqlBackstopFixture _fx;

    public AuditImmutabilityDbPermissionTests(SqlBackstopFixture fx) => _fx = fx;

    [Fact]
    public async Task Acmp_app_role_may_append_and_read_audit_but_not_update_or_delete()
    {
        // Ensure at least one audit row exists (seeded by the privileged sa principal).
        await using (var db = _fx.NewAuditSql())
        {
            if (!await db.AuditEvents.AnyAsync())
            {
                db.AuditEvents.Add(AuditEvent.CreateNext(AuditEvent.Genesis,
                    new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "Test.Seed", "seed", null));
                await db.SaveChangesAsync();
            }
        }

        // Provision a least-priv login in the acmp_app role (idempotent — the collection runs serially).
        await ExecAsync(_fx.ConnectionString, $"""
            IF SUSER_ID('probe') IS NULL CREATE LOGIN probe WITH PASSWORD = '{ProbePassword}', CHECK_POLICY = OFF;
            IF DATABASE_PRINCIPAL_ID('probe') IS NULL CREATE USER probe FOR LOGIN probe;
            IF IS_ROLEMEMBER('acmp_app', 'probe') = 0 ALTER ROLE acmp_app ADD MEMBER probe;
            """);

        var probeConn = new SqlConnectionStringBuilder(_fx.ConnectionString)
        {
            UserID = "probe",
            Password = ProbePassword,
        }.ConnectionString;

        // GRANTed: SELECT + INSERT.
        (await ScalarIntAsync(probeConn, "SELECT COUNT(*) FROM audit.AuditEvents")).Should().BeGreaterThan(0);

        // DENYed: UPDATE + DELETE — the database refuses, regardless of app-layer guards.
        var update = await Assert.ThrowsAsync<SqlException>(() =>
            ExecAsync(probeConn, "UPDATE audit.AuditEvents SET EventType = 'tampered';"));
        update.Message.Should().Contain("permission was denied");

        var delete = await Assert.ThrowsAsync<SqlException>(() =>
            ExecAsync(probeConn, "DELETE FROM audit.AuditEvents;"));
        delete.Message.Should().Contain("permission was denied");
    }

    // P18a Batch 3 (ADR-0031, deployment.md §5): the PRODUCTION runtime login `acmp_svc` is NOT just an acmp_app
    // member — it is also db_datareader + db_datawriter (so it can CRUD the domain tables). db_datawriter GRANTs
    // UPDATE/DELETE on EVERY table, including audit. This proves the DENY on schema::audit still wins for that exact
    // principal shape, and that it is not sysadmin (which WOULD bypass DENY). This is the leg that closes the
    // AC-017/018 "→ P18 least-priv" residual — the residual the acmp_app-only `probe` above does not cover.
    [Fact]
    public async Task Least_priv_svc_login_with_datawriter_is_still_denied_audit_mutation()
    {
        await using (var db = _fx.NewAuditSql())
        {
            if (!await db.AuditEvents.AnyAsync())
            {
                db.AuditEvents.Add(AuditEvent.CreateNext(AuditEvent.Genesis,
                    new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "Test.Seed", "seed", null));
                await db.SaveChangesAsync();
            }
        }

        // Provision a login exactly like the production acmp_svc (mirrors deploy/scripts/sqlserver-init.sh).
        await ExecAsync(_fx.ConnectionString, $"""
            IF SUSER_ID('svcprobe') IS NULL CREATE LOGIN svcprobe WITH PASSWORD = '{ProbePassword}', CHECK_POLICY = OFF;
            IF DATABASE_PRINCIPAL_ID('svcprobe') IS NULL CREATE USER svcprobe FOR LOGIN svcprobe;
            IF IS_ROLEMEMBER('db_datareader','svcprobe') = 0 ALTER ROLE db_datareader ADD MEMBER svcprobe;
            IF IS_ROLEMEMBER('db_datawriter','svcprobe') = 0 ALTER ROLE db_datawriter ADD MEMBER svcprobe;
            GRANT EXECUTE TO svcprobe;
            IF IS_ROLEMEMBER('acmp_app','svcprobe') = 0 ALTER ROLE acmp_app ADD MEMBER svcprobe;
            """);

        var svcConn = new SqlConnectionStringBuilder(_fx.ConnectionString)
        {
            UserID = "svcprobe",
            Password = ProbePassword,
        }.ConnectionString;

        // It IS a datawriter (so DENY has something to override) and is NOT sysadmin (which would bypass DENY).
        (await ScalarIntAsync(svcConn, "SELECT IS_ROLEMEMBER('db_datawriter')")).Should().Be(1);
        (await ScalarIntAsync(svcConn, "SELECT IS_SRVROLEMEMBER('sysadmin')")).Should().Be(0);

        // Readable + appendable...
        (await ScalarIntAsync(svcConn, "SELECT COUNT(*) FROM audit.AuditEvents")).Should().BeGreaterThan(0);

        // ...but the database refuses UPDATE/DELETE on schema::audit despite db_datawriter granting them elsewhere.
        (await Assert.ThrowsAsync<SqlException>(() =>
            ExecAsync(svcConn, "UPDATE audit.AuditEvents SET EventType = 'tampered';")))
            .Message.Should().Contain("permission was denied");
        (await Assert.ThrowsAsync<SqlException>(() =>
            ExecAsync(svcConn, "DELETE FROM audit.AuditEvents;")))
            .Message.Should().Contain("permission was denied");
    }

    // These helpers execute FIXED test DDL/DML against a real SQL container to prove the DB-level DENY; the only
    // interpolation anywhere is the compile-time const ProbePassword. No untrusted input reaches these — the
    // csharp-sqli rule is a false positive here (you cannot parameterize a table-permission probe).
    private static async Task ExecAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection); // nosemgrep
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ScalarIntAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection); // nosemgrep
        return (int)(await command.ExecuteScalarAsync())!;
    }
}
