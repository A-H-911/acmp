using Acmp.Shared.Domain.Entities;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Decisions.Domain;

// A single voter's ballot on a Vote (docs/domain/domain-model.md §Vote). Owned by the Vote root, mutated only through it
// (mirrors DecisionCondition ownership). Seeded at Configure — one row per eligible voter, Choice=null =
// "awaiting". Always attributed (ADR-0010): VoterUserId is the Keycloak sub, VoterName a display snapshot.
// Recused = excluded from the quorum base + the tally (a distinct ballot state, not a choice).
public sealed class Ballot : BaseEntity
{
    private Ballot() { }

    public string VoterUserId { get; private set; } = string.Empty;  // Keycloak sub (eligibility identity)
    public string VoterName { get; private set; } = string.Empty;    // display snapshot
    public string? Choice { get; private set; }                      // an option code or "Abstain"; null = awaiting
    public LocalizedString? Comment { get; private set; }            // optional; mirrored en===ar (guardrail 9)
    public bool Recused { get; private set; }
    public DateTimeOffset? CastAt { get; private set; }

    // D-13 / C-IMM-04 (ADR-0030) — per-ballot tamper-evidence, sealed by the Vote root at Close (see
    // BallotChain / Vote.SealBallotChain). Null until the vote is closed (and on legacy pre-P16 closed votes).
    public string? PreviousHash { get; private set; }
    public string? Hash { get; private set; }

    // Seed an eligible-but-awaiting ballot (at Configure). Eligibility = "has a ballot row".
    internal Ballot(string voterUserId, string voterName)
    {
        if (string.IsNullOrWhiteSpace(voterUserId)) throw new InvalidOperationException("A ballot requires a voter.");
        VoterUserId = voterUserId;
        VoterName = (voterName ?? string.Empty).Trim();
    }

    public bool HasCast => Choice is not null && !Recused;

    // Record (or overwrite) the voter's choice. The one-ballot-per-voter and status guards live on the
    // Vote root; this just applies the value.
    internal void Record(string choice, LocalizedString? comment, DateTimeOffset now)
    {
        Choice = choice;
        Comment = comment;
        CastAt = now;
    }

    internal void Recuse()
    {
        Recused = true;
        Choice = null;
        Comment = null;
        CastAt = null;
    }

    // Stamp the sealed chain links (called once, by the Vote root at Close). Not a mutation of ballot content —
    // the choice/recusal is already frozen by the status guards; this only records the tamper-evidence hashes.
    internal void SealHash(string previousHash, string hash)
    {
        PreviousHash = previousHash;
        Hash = hash;
    }
}
