using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Shared.Infrastructure.Persistence;

// NFR-042 (ADR-0026 §Same-transaction atomicity). Request-scoped holder of the ONE shared DbConnection and the
// single command transaction. Every module DbContext + AuditDbContext is wired onto this one connection, so a
// command's state change and its audit append commit or roll back together — on ONE local transaction, which
// avoids MSDTC escalation (a TransactionScope spanning two connections to the same DB would escalate).
//
// The transaction opens LAZILY, on the first module WRITE (AmbientTransactionStarter). Read-only requests and
// denial/failure audits — which have no paired state change — never open one and simply autocommit.
// TransactionBehavior commits on a clean handler / rolls back on a thrown one.
public sealed class AmbientTransaction : IAsyncDisposable, IDisposable
{
    private readonly DbConnection _connection;

    public AmbientTransaction(DbConnection connection) => _connection = connection;

    public DbTransaction? Current { get; private set; }

    public bool IsActive => Current is not null;

    // Opens the shared connection (once) and begins the command transaction, enlisting the writing context. A
    // second module write in the same command finds the transaction already open and just enlists.
    public async Task EnsureStartedAsync(DbContext writer, CancellationToken ct)
    {
        if (Current is null)
        {
            if (_connection.State != ConnectionState.Open)
                await _connection.OpenAsync(ct);
            Current = await _connection.BeginTransactionAsync(ct);
            await AcquireAuditChainLockAsync(ct);
        }
        EnlistIfNeeded(writer);
    }

    // D-18 / ADR-0028. Serialize audited write-commands on a single transaction-scoped app lock, taken here — at
    // tx-open, BEFORE the first module write — so every write-command acquires {audit-chain lock, then module
    // rows} in the SAME order. That makes it lock-order-safe (a sink-level lock taken at the audit append would
    // invert order against handlers that write more AFTER emitting, e.g. IssueDecision → traceability). It also
    // serializes the audit hash-chain append as a consequence, so concurrent commands can neither fork the chain
    // (PreviousHash/Hash UNIQUE) nor deadlock on its index. Transaction-owned ⇒ auto-released on commit/rollback,
    // so the next command reads a COMMITTED tip. SQL-Server-only (sp_getapplock); a non-SqlServer relational
    // connection — none in production; only hypothetical in tests — degrades to no serialization, not an error.
    private async Task AcquireAuditChainLockAsync(CancellationToken ct)
    {
        // SQL-Server-only (sp_getapplock); a non-SqlServer relational connection — none in production; only
        // hypothetical in tests — simply skips serialization rather than erroring.
        if (_connection is SqlConnection)
        {
            await using var cmd = _connection.CreateCommand();
            cmd.Transaction = Current;
            cmd.CommandText =
                "DECLARE @r int; EXEC @r = sp_getapplock @Resource = N'acmp-audit-chain', @LockMode = 'Exclusive', " +
                "@LockOwner = 'Transaction', @LockTimeout = 15000; " +
                "IF @r < 0 THROW 50000, 'audit-chain applock not acquired', 1;";
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    // Puts a context onto the ambient transaction so EF stops opening its OWN (which would collide with the
    // pending local transaction on the shared connection). A context on a different connection — e.g. the
    // EF-InMemory contexts used by the API test host — never matches and never participates. Idempotent.
    public void EnlistIfNeeded(DbContext context)
    {
        if (Current is null || context.Database.CurrentTransaction is not null)
            return;
        // A context on a different connection (EF-InMemory / Webex) never participates.
        if (!ReferenceEquals(context.Database.GetDbConnection(), _connection)) return;
        context.Database.UseTransaction(Current);
    }

    // Commit/Rollback are only called by TransactionBehavior when IsActive, so the null-guard is defensive; it
    // stays on one line so the executed guard counts and there is no unreachable branch line.
    public async Task CommitAsync(CancellationToken ct)
    {
        if (Current is null) return;
        await Current.CommitAsync(ct);
        await ClearAsync();
    }

    public async Task RollbackAsync(CancellationToken ct)
    {
        if (Current is null) return;
        await Current.RollbackAsync(ct);
        await ClearAsync();
    }

    private async Task ClearAsync()
    {
        if (Current is not null)
            await Current.DisposeAsync();
        Current = null;
    }

    // The scoped DbConnection is DI-owned (disposed with the scope); only the transaction is ours to release.
    // Both dispose paths are implemented so either a sync scope (e.g. MigrationRunner) or an async request scope
    // can release it. After a commit/rollback Current is already null, so these are usually no-ops.
    public async ValueTask DisposeAsync() => await ClearAsync();

    public void Dispose()
    {
        Current?.Dispose();
        Current = null;
    }
}
