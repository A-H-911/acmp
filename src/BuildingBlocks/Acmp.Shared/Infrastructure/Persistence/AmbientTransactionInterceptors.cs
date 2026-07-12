using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Acmp.Shared.Infrastructure.Persistence;

// NFR-042 (ADR-0026). Begins the command transaction on the FIRST module write — the moment a real state change
// is about to be persisted. Keying the begin off a ModuleDbContext SaveChanges (not off arbitrary commands) is
// the gate that keeps read-only requests and denial/failure audits (which never write a module entity) OUT of
// the transaction, so they autocommit and survive a later handler throw (ADR-0026: denials are not rolled back).
// AuditDbContext is not a ModuleDbContext, so an audit append alone never begins a transaction.
public sealed class AmbientTransactionStarter : SaveChangesInterceptor
{
    private readonly AmbientTransaction _ambient;

    public AmbientTransactionStarter(AmbientTransaction ambient) => _ambient = ambient;

    // The app persists exclusively via async SaveChangesAsync, so only the async hook begins the transaction; a
    // (hypothetical) sync save would still get its commands enlisted into an already-open tx by
    // AmbientTransactionInterceptor.CommandCreated — it just would not be the one to OPEN it.
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        if (ShouldBegin(eventData.Context))
            await _ambient.EnsureStartedAsync(eventData.Context!, ct);
        return await base.SavingChangesAsync(eventData, result, ct);
    }

    // Only a relational module write participates. Non-relational (EF-InMemory in the API test host) is skipped
    // so those tests never touch a real SqlConnection.
    private static bool ShouldBegin(DbContext? context) =>
        context is ModuleDbContext && context.Database.IsRelational();
}

// NFR-042 (ADR-0026). Once the command transaction is open, EVERY command on the shared connection must carry
// it — SqlClient rejects an unenlisted command while a local transaction is pending. The starter only enlists
// the writing context; this closes the gap for a subsequent READ on a second context (e.g.
// SqlAuditSink.TipHashAsync's SELECT on AuditDbContext) by enlisting that context and stamping the just-created
// command. CommandCreated fires for EVERY command (sync/async, reader/non-query/scalar) — one hook, not six.
public sealed class AmbientTransactionInterceptor : DbCommandInterceptor
{
    private readonly AmbientTransaction _ambient;

    public AmbientTransactionInterceptor(AmbientTransaction ambient) => _ambient = ambient;

    public override DbCommand CommandCreated(CommandEndEventData eventData, DbCommand result)
    {
        var tx = _ambient.Current;
        if (tx is not null)
        {
            if (eventData.Context is not null)
                _ambient.EnlistIfNeeded(eventData.Context);   // future commands on this context carry the tx
            if (result.Transaction is null && ReferenceEquals(result.Connection, tx.Connection))
                result.Transaction = tx;                       // this command was created before enlistment took hold
        }

        return base.CommandCreated(eventData, result);
    }
}
