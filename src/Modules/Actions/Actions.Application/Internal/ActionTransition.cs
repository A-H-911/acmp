using Acmp.Modules.Actions.Application.Abstractions;
using Acmp.Modules.Actions.Domain;
using Acmp.Shared.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Actions.Application.Internal;

// Shared load → mutate → save → audit path for the simple lifecycle transitions (W14). Each command
// handler supplies only its domain mutation + audit event name; the boilerplate lives here once (DRY).
// (The BCL System.Action<ActionItem, DateTimeOffset> delegate is unambiguous — the entity is ActionItem.)
internal static class ActionTransition
{
    public static async Task ApplyAsync(IActionsDbContext db, IClock clock, IAuditSink audit,
        ICurrentUser user, Guid actionId, string auditEvent,
        Action<ActionItem, DateTimeOffset> mutate, CancellationToken ct)
    {
        var action = await db.Actions.FirstOrDefaultAsync(a => a.PublicId == actionId, ct)
            ?? throw new KeyNotFoundException("Action not found.");

        var (sub, _) = CurrentActor.Of(user);
        mutate(action, clock.UtcNow);
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(auditEvent, sub,
            new { action.PublicId, action.Key, Status = action.Status.ToString() }, ct);
    }
}
