using System.Security.Cryptography;
using System.Text;

namespace Acmp.Shared.Infrastructure.Audit;

// BL-066 (ADR-0009, enriched by ADR-0026) — one row of the immutable, append-only, hash-chained AuditEvent
// log. Every state change across the platform lands here as a tamper-evident record: each row's Hash is
// SHA-256 over its own fields PLUS the previous row's Hash, so altering or deleting any historical row
// breaks the chain from that point forward (verifiable via AuditChainVerifier).
//
// ADR-0026 enriched the row (entity/actor/action/before-after/correlation per audit-and-records.md §1.1) and
// added HashVersion so the payload could grow WITHOUT invalidating history: v1 rows recompute under the
// original formula, v2 rows under the enriched one. The chain LINK (PreviousHash <- prior Hash) is
// version-agnostic, so a v1 row and a v2 row link and verify continuously.
//
// The row is write-once: there are no public setters and no Update/Delete path (SaveChanges only ever Adds).
// Sequence is a DB identity — monotonic, gap-tolerant — and gives a stable order for the chain.
public sealed class AuditEvent
{
    // The chain root's PreviousHash. A UNIQUE index on PreviousHash means this value can appear at most
    // once, so there is exactly one genesis and the chain cannot fork.
    public const string Genesis = "GENESIS";

    private AuditEvent() { }

    // v1 — the original lean row (retained verbatim so historical rows keep verifying).
    private AuditEvent(DateTimeOffset occurredAt, string eventType, string? subject, string? dataJson, string previousHash)
    {
        HashVersion = 1;
        OccurredAt = occurredAt;
        EventType = eventType;
        Subject = subject;
        DataJson = dataJson;
        PreviousHash = previousHash;
        Hash = ComputeHashV1(occurredAt, eventType, subject, dataJson, previousHash);
    }

    // v2 — the enriched row (ADR-0026 / audit-and-records.md §1.1).
    private AuditEvent(string previousHash, DateTimeOffset occurredAt, string action, string? subjectType,
        string? subjectId, string? actorUserId, string? actorRole, string outcome, string? beforeJson,
        string? afterJson, string? correlationId)
    {
        HashVersion = 2;
        OccurredAt = occurredAt;
        Action = action;
        SubjectType = subjectType;
        SubjectId = subjectId;
        ActorUserId = actorUserId;
        ActorRole = actorRole;
        Outcome = outcome;
        BeforeJson = beforeJson;
        AfterJson = afterJson;
        CorrelationId = correlationId;
        PreviousHash = previousHash;
        // Mirror into the legacy display columns so readers that predate enrichment still see a sensible
        // action/actor. These are NOT part of the v2 hash payload.
        EventType = action;
        Subject = actorUserId;
        Hash = ComputeHashV2(this);
    }

    public long Sequence { get; private set; }              // DB identity — chain order
    public int HashVersion { get; private set; } = 1;
    public DateTimeOffset OccurredAt { get; private set; }

    // Legacy (v1) columns — retained for back-compat; on v2 rows they mirror Action / ActorUserId.
    public string EventType { get; private set; } = string.Empty;
    public string? Subject { get; private set; }
    public string? DataJson { get; private set; }

    // Enriched (v2) columns — ADR-0026 / audit-and-records.md §1.1.
    public string? Action { get; private set; }
    public string? SubjectType { get; private set; }
    public string? SubjectId { get; private set; }
    public string? ActorUserId { get; private set; }
    public string? ActorRole { get; private set; }
    // Success | Denied | Failure. ponytail: a string set, not an enum — avoids an EF value-converter for a
    // three-value field; promote to an enum if a fourth value or exhaustive matching is ever needed.
    public string? Outcome { get; private set; }
    public string? BeforeJson { get; private set; }
    public string? AfterJson { get; private set; }
    public string? CorrelationId { get; private set; }

    public string PreviousHash { get; private set; } = Genesis;
    public string Hash { get; private set; } = string.Empty;

    // v1 factory — chains off the current tip hash (or Genesis for the first row). Unchanged signature.
    public static AuditEvent CreateNext(string previousHash, DateTimeOffset occurredAt, string eventType, string? subject, string? dataJson)
        => new(occurredAt, eventType, subject, dataJson, previousHash);

    // v2 factory — the enriched, self-describing governance record.
    public static AuditEvent CreateEnriched(string previousHash, DateTimeOffset occurredAt, string action,
        string? subjectType, string? subjectId, string? actorUserId, string? actorRole, string outcome,
        string? beforeJson, string? afterJson, string? correlationId)
        => new(previousHash, occurredAt, action, subjectType, subjectId, actorUserId, actorRole, outcome,
            beforeJson, afterJson, correlationId);

    // Recompute the hash from this row's own fields, dispatched by version — used to detect content tampering
    // (stored Hash must equal this).
    public string Recompute() => HashVersion switch
    {
        1 => ComputeHashV1(OccurredAt, EventType, Subject, DataJson, PreviousHash),
        2 => ComputeHashV2(this),
        _ => throw new InvalidOperationException($"Unknown audit HashVersion {HashVersion}"),
    };

    // v1 canonical payload — byte-identical to the original ADR-0009 formula (nulls render as empty via
    // string interpolation). DO NOT change: any edit invalidates every pre-ADR-0026 row.
    private static string ComputeHashV1(DateTimeOffset occurredAt, string eventType, string? subject, string? dataJson, string previousHash)
    {
        var payload = $"{occurredAt:O}\n{eventType}\n{subject}\n{dataJson}\n{previousHash}";
        return Sha256Hex(payload);
    }

    // v2 canonical payload — deterministic: a version tag, round-trip ("O") timestamp, ordinal field order,
    // previous hash last. Each nullable field is length-unambiguous via a null-flag prefix ("-" = null,
    // "+" + value = present), so a null and an empty string hash differently and no field value can collide
    // with the null marker regardless of its content.
    private static string ComputeHashV2(AuditEvent e)
    {
        static string F(string? s) => s is null ? "-" : "+" + s;
        var payload = string.Join('\n',
            "v2",
            e.OccurredAt.ToString("O"),
            F(e.Action),
            F(e.SubjectType),
            F(e.SubjectId),
            F(e.ActorUserId),
            F(e.ActorRole),
            F(e.Outcome),
            F(e.BeforeJson),
            F(e.AfterJson),
            F(e.CorrelationId),
            e.PreviousHash);
        return Sha256Hex(payload);
    }

    private static string Sha256Hex(string payload)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
