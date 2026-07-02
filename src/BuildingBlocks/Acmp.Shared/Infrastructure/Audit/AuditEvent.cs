using System.Security.Cryptography;
using System.Text;

namespace Acmp.Shared.Infrastructure.Audit;

// BL-066 (ADR-0009) — one row of the immutable, append-only, hash-chained AuditEvent log. This is the
// durable store the IAuditSink seam was always meant to bind to; it replaces the P4 interim Serilog-only
// sink. Every state change across the platform lands here as a tamper-evident record: each row's Hash is
// SHA-256 over its own fields PLUS the previous row's Hash, so altering or deleting any historical row
// breaks the chain from that point forward (verifiable via AuditChainVerifier).
//
// The row is write-once: there are no public setters and no Update/Delete path (SaveChanges only ever
// Adds). Sequence is a DB identity — monotonic, gap-tolerant — and gives a stable order for the chain.
public sealed class AuditEvent
{
    // The chain root's PreviousHash. A UNIQUE index on PreviousHash means this value can appear at most
    // once, so there is exactly one genesis and the chain cannot fork.
    public const string Genesis = "GENESIS";

    private AuditEvent() { }

    private AuditEvent(DateTimeOffset occurredAt, string eventType, string? subject, string? dataJson, string previousHash)
    {
        OccurredAt = occurredAt;
        EventType = eventType;
        Subject = subject;
        DataJson = dataJson;
        PreviousHash = previousHash;
        Hash = ComputeHash(occurredAt, eventType, subject, dataJson, previousHash);
    }

    public long Sequence { get; private set; }              // DB identity — chain order
    public DateTimeOffset OccurredAt { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string? Subject { get; private set; }
    public string? DataJson { get; private set; }
    public string PreviousHash { get; private set; } = Genesis;
    public string Hash { get; private set; } = string.Empty;

    // Build the next link, chaining off the current tip hash (or Genesis for the first row).
    public static AuditEvent CreateNext(string previousHash, DateTimeOffset occurredAt, string eventType, string? subject, string? dataJson)
        => new(occurredAt, eventType, subject, dataJson, previousHash);

    // Recompute the hash from this row's own fields — used to detect content tampering (stored Hash must equal this).
    public string Recompute() => ComputeHash(OccurredAt, EventType, Subject, DataJson, PreviousHash);

    private static string ComputeHash(DateTimeOffset occurredAt, string eventType, string? subject, string? dataJson, string previousHash)
    {
        // Round-trip ("O") timestamp so the canonical form is culture- and precision-stable.
        var payload = $"{occurredAt:O}\n{eventType}\n{subject}\n{dataJson}\n{previousHash}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
