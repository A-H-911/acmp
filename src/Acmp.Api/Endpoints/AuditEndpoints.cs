using Acmp.Shared.Application.Pagination;
using Acmp.Shared.Authorization;
using Acmp.Shared.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Api.Endpoints;

// AC-017/019/020 (INV-005, ADR-0026/0027) — the Auditor's read + on-demand chain-verify over the immutable,
// hash-chained AuditEvent log. Read-only BY CONSTRUCTION: there are no write routes, and AuditEvent has no
// setters/delete path (AC-018 is met structurally + by the verifier's tamper tests, not by attempting a
// blocked write). Gated by Policies.AuditRead = {Auditor, Chairman, Secretary}; Administrator is deliberately
// excluded on SoD-5 grounds (ADR-0027 supersedes the FR-153 role clause) -> 403; no token -> 401.
//
// DEVIATION from the plan's "GetAuditEventsQuery + MediatR handler" wording: this is a pure read with no
// validation and no cross-module concern, so it injects AuditDbContext directly into the endpoint lambda
// (the AdminEndpoints precedent) instead of routing through MediatR — which would only drag it through the
// (no-op-for-a-read) AuthorizationBehavior + TransactionBehavior. ADR-0001 is respected: AuditDbContext is
// shared infrastructure, not a business module.
//
// The store holds TWO row shapes: enriched v2 rows (governed state changes — Action/SubjectType/SubjectId/
// ActorUserId/Outcome/Before/After populated) and lean v1 rows (system/integration/authZ events + pre-
// enrichment history — EventType/Subject populated, enriched columns null). The DTO normalizes across both
// (Action ?? EventType; Actor = ActorUserId ?? Subject) so a mixed log reads uniformly.
public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit").WithTags("Audit")
            .RequireAuthorization(Policies.AuditRead);

        // The audit register — filtered/paged, newest-first (Sequence DESC: a reviewer wants the latest
        // activity first; the chain-order ASC scan is the verify endpoint's job). entityType matches the CLR
        // aggregate name stored in SubjectType (v2 rows only); actor/action match across both row shapes via
        // COALESCE; from/to bound OccurredAt.
        group.MapGet("/", async (
            AuditDbContext db, CancellationToken ct,
            string? entityType = null, string? actor = null, string? action = null,
            DateTimeOffset? from = null, DateTimeOffset? to = null,
            int page = 1, int pageSize = 25) =>
        {
            var q = db.AuditEvents.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(entityType))
                q = q.Where(e => e.SubjectType == entityType);
            if (!string.IsNullOrWhiteSpace(actor))
                q = q.Where(e => (e.ActorUserId ?? e.Subject) == actor);
            if (!string.IsNullOrWhiteSpace(action))
                q = q.Where(e => (e.Action ?? e.EventType) == action);
            if (from is not null)
                q = q.Where(e => e.OccurredAt >= from.Value);
            if (to is not null)
                q = q.Where(e => e.OccurredAt <= to.Value);

            var total = await q.CountAsync(ct);
            var pg = page <= 0 ? 1 : page;
            var size = pageSize <= 0 ? 25 : pageSize;

            var items = await q
                .OrderByDescending(e => e.Sequence)
                .Skip((pg - 1) * size)
                .Take(size)
                .Select(e => new AuditEventDto(
                    e.Sequence, e.OccurredAt, e.HashVersion,
                    e.Action ?? e.EventType,
                    e.SubjectType, e.SubjectId,
                    e.ActorUserId ?? e.Subject, e.ActorRole, e.Outcome,
                    e.BeforeJson, e.AfterJson, e.CorrelationId))
                .ToListAsync(ct);

            return Results.Ok(new PagedResult<AuditEventDto>(items, total, pg, size));
        });

        // On-demand chain integrity check (AC-019). ponytail: full-scan verify — loads the whole ordered log
        // and recomputes every hash. Fine at this deployment's scale (<=20 users, low write rate); batch by
        // Sequence window if the log ever exceeds ~Nk rows.
        group.MapGet("/verify", async (AuditDbContext db, CancellationToken ct) =>
        {
            var events = await db.AuditEvents.AsNoTracking().OrderBy(e => e.Sequence).ToListAsync(ct);
            var result = AuditChainVerifier.Verify(events);
            return Results.Ok(new AuditVerifyDto(result.IsValid, result.BrokenAtSequence, result.Reason));
        });

        return app;
    }

    // Surfaces both row shapes; enriched fields are nullable (null on v1 rows). Action/Actor are pre-
    // normalized so the register renders one column regardless of shape.
    public sealed record AuditEventDto(
        long Sequence, DateTimeOffset OccurredAt, int HashVersion,
        string Action, string? SubjectType, string? SubjectId,
        string? Actor, string? ActorRole, string? Outcome,
        string? BeforeJson, string? AfterJson, string? CorrelationId);

    public sealed record AuditVerifyDto(bool IsValid, long? BrokenAtSequence, string? Reason);
}
