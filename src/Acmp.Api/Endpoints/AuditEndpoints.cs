using System.Globalization;
using System.Text;
using System.Text.Json;
using Acmp.Api.Infrastructure;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Pagination;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Membership;
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
    private const string CsvFormat = "csv";
    private const string JsonFormat = "json";

    // Camel-cased to match every other JSON payload this API emits, so an exported file and an API
    // response describe the same row the same way. Indented because the consumer of an export is a
    // person or a spreadsheet, not a parser optimising for bytes.
    private static readonly JsonSerializerOptions ExportJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    // The register and the export MUST filter identically — a compliance export that silently covers a
    // different set from the screen the reviewer filtered is worse than no export. One predicate, two
    // callers, so the two cannot drift and Export_and_register_agree_on_the_same_filters can compare them.
    private static IQueryable<AuditEvent> ApplyFilters(
        IQueryable<AuditEvent> q, string? entityType, string? actor, string? action,
        DateTimeOffset? from, DateTimeOffset? to)
    {
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
        return q;
    }

    // RFC 4180. ponytail: a local writer rather than a CSV package — this is the only server-side CSV in
    // the product and the whole grammar that matters is "quote it, and double any quote inside".
    private static string ToCsv(IReadOnlyList<AuditExportRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("sequence,occurredAt,hashVersion,action,subjectType,subjectId,actor,actorRole,outcome,beforeJson,afterJson,correlationId");
        foreach (var r in rows)
        {
            sb.Append(r.Sequence.ToString(CultureInfo.InvariantCulture)).Append(',')
              // Round-trip ("O") and invariant throughout: an export is machine-read downstream, and a
              // locale-formatted timestamp or number in a compliance file is a defect, not localization.
              // This is the deliberate counterpart to NFR-037 — WBS-24.4 localizes what a PERSON READS on
              // screen; a file another system parses must not move with the reader's locale.
              .Append(Csv(r.OccurredAt.ToString("O", CultureInfo.InvariantCulture))).Append(',')
              .Append(r.HashVersion.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(Csv(r.Action)).Append(',')
              .Append(Csv(r.SubjectType)).Append(',')
              .Append(Csv(r.SubjectId)).Append(',')
              .Append(Csv(r.Actor)).Append(',')
              .Append(Csv(r.ActorRole)).Append(',')
              .Append(Csv(r.Outcome)).Append(',')
              .Append(Csv(r.BeforeJson)).Append(',')
              .Append(Csv(r.AfterJson)).Append(',')
              .Append(Csv(r.CorrelationId))
              .Append('\n');
        }
        return sb.ToString();
    }

    // Always quote a non-empty field. The before/after columns hold raw JSON, which is full of commas and
    // quotes, so conditional quoting would be one missed branch away from a file that parses into the
    // wrong columns — and nothing downstream would report an error, it would just be wrong.
    private static string Csv(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit").WithTags("Audit")
            .RequireAuthorization(Policies.AuditRead);

        // The audit register — filtered/paged, newest-first (Sequence DESC: a reviewer wants the latest
        // activity first; the chain-order ASC scan is the verify endpoint's job). entityType matches the CLR
        // aggregate name stored in SubjectType (v2 rows only); actor/action match across both row shapes via
        // COALESCE; from/to bound OccurredAt.
        group.MapGet("/", async (
            AuditDbContext db, ICommitteeDirectory directory, CancellationToken ct,
            string? entityType = null, string? actor = null, string? action = null,
            DateTimeOffset? from = null, DateTimeOffset? to = null,
            int page = 1, int pageSize = 25) =>
        {
            var q = ApplyFilters(db.AuditEvents.AsNoTracking(), entityType, actor, action, from, to);

            var total = await q.CountAsync(ct);
            var pg = page <= 0 ? 1 : page;
            var size = PageSize.Clamp(pageSize);   // DEF-104: cap the caller-supplied page

            var items = await q
                .OrderByDescending(e => e.Sequence)
                .Skip((pg - 1) * size)
                .Take(size)
                .Select(e => new AuditEventDto(
                    e.Sequence, e.OccurredAt, e.HashVersion,
                    e.Action ?? e.EventType,
                    e.SubjectType, e.SubjectId,
                    e.ActorUserId ?? e.Subject, e.ActorRole, e.Outcome,
                    e.BeforeJson, e.AfterJson, e.CorrelationId, null))
                .ToListAsync(ct);

            // Resolve actor subjects to people. The register previously rendered a bare Keycloak GUID in
            // the "actor" column, which is not an audit control a human can read — a reviewer cannot tell
            // who acted without a second lookup they have no UI for.
            //
            // Resolved through the ICommitteeDirectory port, never by reading Membership's tables (ADR-0001),
            // and AFTER materialisation because the join is across a module boundary, not inside the query.
            // ResolveDisplayNamesAsync deliberately includes DISABLED members: AC-058 keeps their records so
            // historical attribution survives, so an active-only lookup would blank exactly the departed-member
            // rows that matter most.
            //
            // Actor is KEPT alongside the name rather than replaced — the subject is the forensic identity and
            // display names are neither unique nor stable.
            var names = await directory.ResolveDisplayNamesAsync(
                items.Select(i => i.Actor).Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a!).ToArray(), ct);

            var enriched = items
                .Select(i => i.Actor is not null && names.TryGetValue(i.Actor, out var name)
                    ? i with { ActorName = name }
                    : i)
                .ToList();

            return Results.Ok(new PagedResult<AuditEventDto>(enriched, total, pg, size));
        });

        /*
         * WBS-24.6 (DW-035 / FR-154; DEC-081 d2 / SC-036) — the audit-log export.
         *
         * ROLES. FR-154's own text says "accessible only to Auditor and Administrator". That clause is
         * SUPERSEDED by ADR-0027, which decides audit read, search, EXPORT and delete are {Auditor,
         * Chairman, Secretary} with Administrator excluded on SoD-5 grounds — it names exporting
         * explicitly. The group's Policies.AuditRead already IS that set, so the export inherits the
         * correct authorization by living here rather than by re-deriving it.
         *
         * ReportExport is required IN ADDITION because SEC-081 gates audit export on `Report.Export` +
         * `Audit.Read`. It changes no behaviour — ReportExport's role set is a strict superset of
         * AuditRead's, so the intersection is AuditRead's three — and it is written anyway so that a
         * later widening of ReportExport cannot silently widen THIS endpoint, and so the code says what
         * the control says. Do not "simplify" it away.
         *
         * ⛔ NO PageSize.Clamp HERE, AND THAT IS THE POINT. DEF-104 taught that every paged read must cap
         * a caller-supplied page size, and this is the one endpoint where copying that pattern would be
         * the defect: an export is the compliance artifact an external auditor reads, and a silently
         * truncated one is DEF-103's shape on the worst possible surface. Truncation would be indis-
         * tinguishable from "those rows do not exist". Export_is_not_truncated_by_the_paged_read_cap
         * exists to fail if anyone routes this back through the register's paging.
         *
         * C-AUDIT-08: "every report/data export is an audited sensitive event (who, scope, volume)" —
         * emitted below, after the rows are counted, so the volume recorded is the volume delivered.
         * C-API-03 names export endpoints for rate limiting; RateLimitPolicies.Export is the tightest
         * of the four windows. C-INS-01's anomaly ALERT on that volume is NOT here: it has no baseline
         * to threshold against yet, and DW-087 carries it with the volume data starting to accumulate
         * from this event.
         */
        group.MapGet("/export", async (
            AuditDbContext db, ICurrentUser user, IAuditSink audit, CancellationToken ct,
            string format = CsvFormat, string? entityType = null, string? actor = null,
            string? action = null, DateTimeOffset? from = null, DateTimeOffset? to = null) =>
        {
            // Boundary validation (guardrail: validate at the edge, fail fast with a clear message).
            var fmt = (format ?? string.Empty).Trim().ToLowerInvariant();
            if (fmt is not (CsvFormat or JsonFormat))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["format"] = [$"Format must be '{CsvFormat}' or '{JsonFormat}'."],
                });
            }

            var rows = await ApplyFilters(db.AuditEvents.AsNoTracking(), entityType, actor, action, from, to)
                // Chain order, oldest-first: an export is read as a RECORD rather than as a feed, and the
                // hash chain runs in Sequence order — so a recipient can re-verify the file's own ordering.
                // This is deliberately the opposite of the register's newest-first.
                .OrderBy(e => e.Sequence)
                .Select(e => new AuditExportRow(
                    e.Sequence, e.OccurredAt, e.HashVersion,
                    e.Action ?? e.EventType, e.SubjectType, e.SubjectId,
                    e.ActorUserId ?? e.Subject, e.ActorRole, e.Outcome,
                    e.BeforeJson, e.AfterJson, e.CorrelationId))
                .ToListAsync(ct);

            // C-AUDIT-08 — who, scope and volume. The scope is every filter as received, so a reviewer can
            // reconstruct exactly which slice of the log left the system; volume is the delivered row count.
            // ⚠ No display-name resolution on this path: the export carries the forensic ActorUserId only.
            // The register resolves names AFTER materialisation through ICommitteeDirectory, which is a
            // cross-module call per row-set; an export is unbounded, and a name is neither unique nor stable
            // enough to be the identity in a compliance file. AC-152 states this exclusion rather than
            // leaving it to be discovered.
            await audit.EmitAsync("audit.exported", user.UserId, new
            {
                Format = fmt,
                EntityType = entityType,
                Actor = actor,
                Action = action,
                From = from,
                To = to,
                RowCount = rows.Count,
            }, ct);

            var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

            if (fmt == JsonFormat)
                return Results.File(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rows, ExportJson)),
                    "application/json; charset=utf-8", $"acmp-audit-{stamp}.json");

            // ⚠ THE BOM IS LOAD-BEARING, NOT DECORATION. Excel reads a BOM-less UTF-8 CSV in the system
            // codepage, which turns every Arabic actor name and every Arabic value inside a before/after
            // JSON blob into mojibake — in a bilingual product that is a corrupted compliance artifact,
            // not a cosmetic bug. ReportsPage.tsx prepends the same "﻿" for the same reason.
            return Results.File(Encoding.UTF8.GetBytes('﻿' + ToCsv(rows)),
                "text/csv; charset=utf-8", $"acmp-audit-{stamp}.csv");
        })
        .RequireAuthorization(Policies.ReportExport)
        .RequireRateLimiting(RateLimitPolicies.Export);

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
        string? BeforeJson, string? AfterJson, string? CorrelationId,
        // The actor's display name, resolved via ICommitteeDirectory. NULL when the subject has no member
        // row (system/integration actors) or on a v1 row with no actor — the client must fall back to Actor,
        // which is kept because the subject is the forensic identity; display names are neither unique nor
        // stable. Appended last so the addition is additive for existing consumers.
        string? ActorName = null);

    public sealed record AuditVerifyDto(bool IsValid, long? BrokenAtSequence, string? Reason);

    // The exported row (WBS-24.6). Deliberately NOT AuditEventDto: that record carries ActorName, which
    // the export does not resolve, and an optional field left permanently null in every exported file
    // would read as "this actor has no name" rather than "this format does not carry names". Same twelve
    // forensic columns in the same order as the CSV header, so the two formats are the same record.
    public sealed record AuditExportRow(
        long Sequence, DateTimeOffset OccurredAt, int HashVersion,
        string Action, string? SubjectType, string? SubjectId,
        string? Actor, string? ActorRole, string? Outcome,
        string? BeforeJson, string? AfterJson, string? CorrelationId);
}
