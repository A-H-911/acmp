using Acmp.Modules.Risks.Application.Abstractions;
using Acmp.Modules.Risks.Domain;
using Acmp.Shared.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Risks.Application.Internal;

// Shared load → mutate → save → audit path for the risk transitions that touch the aggregate (and its owned
// mitigations) without an extra side-effect (W15 begin-mitigation, close, add/advance mitigation). Each
// handler supplies only its domain mutation + audit event name; the boilerplate lives here once (DRY). The
// query is TRACKING (default) so EF loads the owned Mitigation collection and persists changes to it.
internal static class RiskTransition
{
    public static async Task ApplyAsync(IRisksDbContext db, IClock clock, IAuditSink audit,
        ICurrentUser user, Guid riskId, string auditEvent,
        Action<Risk, DateTimeOffset> mutate, CancellationToken ct)
    {
        var risk = await db.Risks.FirstOrDefaultAsync(r => r.PublicId == riskId, ct)
            ?? throw new KeyNotFoundException("Risk not found.");

        var (sub, _) = CurrentActor.Of(user);
        mutate(risk, clock.UtcNow);
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(auditEvent, sub,
            new { risk.PublicId, risk.Key, Status = risk.Status.ToString() }, ct);
    }
}
