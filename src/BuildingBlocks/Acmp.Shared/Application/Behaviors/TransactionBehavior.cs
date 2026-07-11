using Acmp.Shared.Infrastructure.Persistence;
using MediatR;

namespace Acmp.Shared.Application.Behaviors;

// NFR-042 (ADR-0026 §Same-transaction atomicity). Innermost behavior — it wraps ONLY the handler, so the
// authorization/validation denials that emit-then-throw in the OUTER behaviors run before any transaction and
// autocommit their audit rows. The transaction itself is opened lazily by the first module write inside the
// handler (AmbientTransactionStarter); this behavior just closes it: commit when the handler returns cleanly,
// roll back when it throws. A read-only request or an in-handler denial that never wrote a module entity leaves
// no transaction open, so both commit/rollback are no-ops (IsActive == false) — nothing to undo.
public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly AmbientTransaction _ambient;

    public TransactionBehavior(AmbientTransaction ambient) => _ambient = ambient;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        try
        {
            var response = await next();
            if (_ambient.IsActive)
                await _ambient.CommitAsync(ct);
            return response;
        }
        catch
        {
            if (_ambient.IsActive)
                await _ambient.RollbackAsync(ct);
            throw;
        }
    }
}
