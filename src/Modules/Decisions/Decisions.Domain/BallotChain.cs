using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Acmp.Modules.Decisions.Domain;

// D-13 / C-IMM-04 (docs/domain/security-controls.md, ADR-0030) — per-ballot tamper-evidence for a closed vote.
// Deliberately mirrors the AuditEvent hash-chain (Acmp.Shared.Infrastructure.Audit / AuditChainVerifier):
// SHA-256 hex, a version tag, null-flag-prefixed fields so null and "" hash differently, previous hash last,
// a genesis root. The Vote root seals the chain at Close over ALL ballot rows in a deterministic order (by
// voter sub) — so a direct-SQL edit, insert, delete, or reorder of vote_ballots on a *closed* vote is
// detectable. That is the exact gap the vote state-change AuditEvent chain does not cover (it chains audit
// rows, not ballot rows). No per-ballot signatures: proportional to <=20 trusted users on-prem, no PKI
// (ADR-0030); tamper-evidence, not tamper-proofing, is the goal.
public static class BallotChain
{
    public const string Genesis = "0000000000000000000000000000000000000000000000000000000000000000"; // 64 zeros

    // Canonical, version-tagged, null-unambiguous payload; previousHash last. DO NOT change once rows are
    // sealed — any edit invalidates every sealed ballot (same contract as AuditEvent.ComputeHashV2).
    public static string ComputeHash(int sequence, string voterUserId, string? choice, bool recused,
        DateTimeOffset? castAt, string? commentEn, string? commentAr, string previousHash)
    {
        static string F(string? s) => s is null ? "-" : "+" + s;
        var payload = string.Join('\n',
            "bc1",
            sequence.ToString(CultureInfo.InvariantCulture),
            voterUserId,
            F(choice),
            recused ? "R" : "-",
            castAt is null ? "-" : castAt.Value.ToString("O"),
            F(commentEn),
            F(commentAr),
            previousHash);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

// Result of a per-ballot chain check (mirrors AuditChainVerifier.Result). An UNSEALED vote — still open, or a
// legacy pre-P16 closed vote — is not a tamper: IsValid stays true so the integrity job does not alert on it.
public readonly record struct BallotChainResult(bool IsValid, bool IsSealed, int? BrokenAtIndex, string? Reason)
{
    public static readonly BallotChainResult Valid = new(true, true, null, null);
    public static readonly BallotChainResult Unsealed = new(true, false, null, null);

    public static BallotChainResult Broken(int index, string reason) => new(false, true, index, reason);
}
