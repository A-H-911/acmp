using Acmp.Modules.Decisions.Domain.Enums;
using Acmp.Modules.Decisions.Domain.Events;
using Acmp.Shared.Domain.Entities;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Decisions.Domain;

// The Vote aggregate root (docs/domain/domain-model.md §Vote, docs/domain/entity-lifecycles.md §4, ADR-0010) — a configurable, always-attributed ballot
// on a topic/decision. OWNED BY the Decisions module (like MinutesOfMeeting lives inside Meetings). Identity
// to other modules is by id only (TopicId = Topic.PublicId; MeetingId = Meeting.PublicId) — never an EF
// navigation, so the module boundary holds (ADR-0001).
//
// Lifecycle Configured → Open → Closed → Ratified, strictly forward-only. Quorum is live attendance-linked:
// MinPresent is checked at Open (present-eligible count comes from the Meetings quorum seam, injected by the
// handler — the domain just compares), MinCast at Close (non-recused ballots cast, AC-024). After Close the
// ballots + tally are FROZEN (AC-025); every re-transition throws (AC-026).
//
// ponytail: immutability is enforced by no-public-mutators + RowVersion + the status guards (exactly like
// Decision). Every vote STATE CHANGE (Configured/Open/Close/Ratify) is hash-chained via the durable
// AuditEvent store (BL-066, ADR-0009). P16/D-13 (ADR-0030) adds a per-ballot crypto hash-chain, sealed at
// Close (see SealBallotChain / VerifyBallotChain), so a direct-SQL edit of the frozen vote_ballots rows is
// also detectable — the gap the state-change audit chain does not cover.
public sealed class Vote : AuditableEntity
{
    public const string AbstainChoice = "Abstain";

    private readonly List<Ballot> _ballots = new();

    private Vote() { }

    // Optimistic-concurrency token (SQL rowversion). A stale write throws DbUpdateConcurrencyException → API 409 (docs/domain/data-architecture.md §1.5, ADR-0018).
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public string Key { get; private set; } = string.Empty;   // VOTE-YYYY-### (human-readable display key)
    public Guid TopicId { get; private set; }                 // Topic.PublicId
    public Guid? MeetingId { get; private set; }              // Meeting.PublicId (nullable — a vote may be run outside a meeting)
    public VoteStatus Status { get; private set; }
    public IReadOnlyList<string> Options { get; private set; } = Array.Empty<string>(); // choice codes; serialized inline
    public bool AllowAbstain { get; private set; }
    public QuorumRule QuorumRule { get; private set; } = new(0, 1); // owned value object (MinPresent/MinCast)
    public VoteTally? Tally { get; private set; }             // frozen at Close; serialized inline
    public string? ResultSummary { get; private set; }
    public DateTimeOffset? OpenedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    // D-13 / C-IMM-04 (ADR-0030): when the per-ballot hash chain was sealed (at Close). Null = unsealed —
    // either still open, or a legacy pre-P16 closed vote (the integrity job skips those, never alerts).
    public DateTimeOffset? ChainSealedAt { get; private set; }

    // SoD-3 (docs/domain/entity-lifecycles.md §4): the actor who CLOSED the vote is the counter of record (Option A — no separate
    // co-attester field). The decision-issue path checks chair ≠ this counter (AC-015/016).
    public string? CounterUserId { get; private set; }        // Keycloak sub of the closer
    public string? CounterName { get; private set; }          // display snapshot

    public IReadOnlyCollection<Ballot> Ballots => _ballots.AsReadOnly();

    // W11: configure the ballot. Configured is still mutable-by-replacement (no field setters) until Open.
    // Guards: a topic is required; ≥2 options; MinCast ≥ 1. One awaiting Ballot is seeded per eligible voter
    // (eligibility = "has a ballot row").
    public static Vote Configure(string key, Guid topicId, Guid? meetingId, IEnumerable<string> options,
        bool allowAbstain, QuorumRule quorumRule, IEnumerable<VoteEligibleVoter> eligibleVoters,
        string actorSub, DateTimeOffset now)
    {
        if (topicId == Guid.Empty) throw new InvalidOperationException("A vote must reference a topic.");
        if (quorumRule is null) throw new InvalidOperationException("A quorum rule is required.");

        var optionList = (options ?? Enumerable.Empty<string>())
            .Select(o => (o ?? string.Empty).Trim()).Where(o => o.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        if (optionList.Count < 2) throw new InvalidOperationException("A vote requires at least two options.");
        if (quorumRule.MinCast < 1) throw new InvalidOperationException("The cast quorum (MinCast) must be at least 1.");
        if (quorumRule.MinPresent < 0) throw new InvalidOperationException("The present quorum (MinPresent) cannot be negative.");

        var vote = new Vote
        {
            Key = key.Trim(),
            TopicId = topicId,
            MeetingId = meetingId,
            Status = VoteStatus.Configured,
            Options = optionList,
            AllowAbstain = allowAbstain,
            QuorumRule = quorumRule,
        };

        foreach (var voter in eligibleVoters ?? Enumerable.Empty<VoteEligibleVoter>())
            vote.SeedVoter(voter.UserId, voter.Name);
        if (vote._ballots.Count == 0) throw new InvalidOperationException("A vote requires at least one eligible voter.");

        vote.Raise(new VoteConfiguredEvent(vote.PublicId, vote.Key, topicId, now));
        return vote;
    }

    private void SeedVoter(string userId, string name)
    {
        if (_ballots.Any(b => string.Equals(b.VoterUserId, userId, StringComparison.Ordinal))) return; // idempotent
        _ballots.Add(new Ballot(userId, name));
    }

    // W11: open voting. Configured → Open; the config locks (AC-021 — no config mutators after Open). The
    // present-quorum gate uses the eligible-and-present count resolved by the handler from the linked meeting;
    // when there is no linked meeting the handler passes the present count it can compute (see OpenVoteHandler).
    public void Open(string actorSub, int presentEligibleCount, DateTimeOffset now)
    {
        RequireStatus(VoteStatus.Configured);
        if (presentEligibleCount < QuorumRule.MinPresent)
            throw new InvalidOperationException(
                $"Present quorum not met: {presentEligibleCount} of {QuorumRule.MinPresent} eligible voters present.");

        Status = VoteStatus.Open;
        OpenedAt = now;
        Raise(new VoteOpenedEvent(PublicId, Key, TopicId, now));
    }

    // W11: cast a ballot (first submission). AC-022: a second cast is REJECTED — the handler audits the denial.
    // Use ChangeBallot to change a vote while Open (design's "change until close").
    public void Cast(string voterSub, string choice, LocalizedString? comment, DateTimeOffset now)
    {
        var ballot = RequireEligibleBallot(voterSub);
        if (ballot.HasCast) throw new InvalidOperationException("You have already voted.");
        ballot.Record(ValidateChoice(choice), comment, now);
        Raise(new BallotCastEvent(PublicId, Key, voterSub, now));
    }

    // W11 (design "you can change your vote until voting closes"): overwrite an existing ballot while Open.
    public void ChangeBallot(string voterSub, string choice, LocalizedString? comment, DateTimeOffset now)
    {
        var ballot = RequireEligibleBallot(voterSub);
        ballot.Record(ValidateChoice(choice), comment, now);
    }

    // W11: recuse a voter — excluded from the quorum base + the tally. Distinct from a choice (a recused
    // voter has no vote counted). Only while Open.
    public void Recuse(string voterSub, DateTimeOffset now)
    {
        var ballot = RequireEligibleBallot(voterSub);
        ballot.Recuse();
    }

    // W11: close voting. Open → Closed. AC-024: non-recused ballots cast must meet MinCast, else the close is
    // rejected and the vote stays Open. Freezes the tally (per-option + abstain counts). Records the closer as
    // the counter of record (SoD-3, Option A).
    public void Close(string actorSub, string actorName, DateTimeOffset now)
    {
        RequireStatus(VoteStatus.Open);

        var castCount = _ballots.Count(b => b.HasCast);
        if (castCount < QuorumRule.MinCast)
            throw new InvalidOperationException(
                $"Quorum not met: {castCount} of {QuorumRule.MinCast} required votes cast.");

        Tally = ComputeTally();
        ResultSummary = string.Join(", ", Tally.OptionCounts.Select(kv => $"{kv.Key}: {kv.Value}")
            .Concat(Tally.AbstainCount > 0 ? new[] { $"{AbstainChoice}: {Tally.AbstainCount}" } : Array.Empty<string>()));
        Status = VoteStatus.Closed;
        ClosedAt = now;
        CounterUserId = actorSub;
        CounterName = (actorName ?? string.Empty).Trim();
        SealBallotChain(now); // D-13 / ADR-0030: freeze the per-ballot tamper-evidence chain
        Raise(new VoteClosedEvent(PublicId, Key, now));
    }

    // The tally is a pure function of the cast, non-recused ballots — extracted so the integrity job can
    // recompute it and compare against the frozen snapshot (see VerifyTally). Do NOT call after Close to
    // mutate Tally (AC-025 freezes it); this only reads _ballots.
    public VoteTally ComputeTally()
    {
        var optionCounts = Options.ToDictionary(o => o, _ => 0, StringComparer.Ordinal);
        var abstain = 0;
        var castCount = 0;
        foreach (var ballot in _ballots.Where(b => b.HasCast))
        {
            castCount++;
            if (string.Equals(ballot.Choice, AbstainChoice, StringComparison.Ordinal)) abstain++;
            else if (ballot.Choice is not null && optionCounts.ContainsKey(ballot.Choice)) optionCounts[ballot.Choice]++;
        }

        return new VoteTally(optionCounts, abstain, castCount);
    }

    // D-13 / ADR-0030: seal the per-ballot chain over ALL ballot rows in a deterministic order (by voter sub —
    // stable + infra-independent, so the verifier reproduces it without trusting EF load order). Called once,
    // from Close.
    private void SealBallotChain(DateTimeOffset sealedAt)
    {
        var ordered = _ballots.OrderBy(b => b.VoterUserId, StringComparer.Ordinal).ToList();
        var previousHash = BallotChain.Genesis;
        for (var i = 0; i < ordered.Count; i++)
        {
            var b = ordered[i];
            var hash = BallotChain.ComputeHash(i, b.VoterUserId, b.Choice, b.Recused, b.CastAt,
                b.Comment?.En, b.Comment?.Ar, previousHash);
            b.SealHash(previousHash, hash);
            previousHash = hash;
        }

        ChainSealedAt = sealedAt;
    }

    // D-13 tamper-check: re-derive the sealed chain and report the first ballot that fails. An unsealed vote
    // (still open, or a legacy pre-P16 closed vote) returns Unsealed — not a tamper. Reused by the nightly
    // integrity job (C-INS-02).
    public BallotChainResult VerifyBallotChain()
    {
        if (ChainSealedAt is null) return BallotChainResult.Unsealed;

        var ordered = _ballots.OrderBy(b => b.VoterUserId, StringComparer.Ordinal).ToList();
        var previousHash = BallotChain.Genesis;
        for (var i = 0; i < ordered.Count; i++)
        {
            var b = ordered[i];
            var expected = BallotChain.ComputeHash(i, b.VoterUserId, b.Choice, b.Recused, b.CastAt,
                b.Comment?.En, b.Comment?.Ar, previousHash);
            if (b.Hash != expected)
                return BallotChainResult.Broken(i, "ballot content tampered (stored hash != recomputed)");
            if (b.PreviousHash != previousHash)
                return BallotChainResult.Broken(i, "ballot chain broken (previous hash mismatch)");
            previousHash = b.Hash;
        }

        return BallotChainResult.Valid;
    }

    // D-16 / C-INS-02: the frozen Tally must still match a recompute from the ballots — a forged tally_json
    // that no longer agrees with the (chain-verified) ballots is detectable. Field-by-field: VoteTally value-
    // equality is reference-based over its Dictionary (see VoteTally). A never-closed vote (Tally null) → true.
    public bool VerifyTally()
    {
        if (Tally is null) return true;

        var recomputed = ComputeTally();
        if (recomputed.AbstainCount != Tally.AbstainCount || recomputed.CastCount != Tally.CastCount) return false;
        if (recomputed.OptionCounts.Count != Tally.OptionCounts.Count) return false;
        foreach (var kv in recomputed.OptionCounts)
            if (!Tally.OptionCounts.TryGetValue(kv.Key, out var stored) || stored != kv.Value) return false;

        return true;
    }

    // W11/W12: ratify. Closed → Ratified. Called when the linked decision is chair-approved (IssueDecision).
    // Adds no mutation to the frozen ballots/tally (docs/domain/entity-lifecycles.md §4).
    public void Ratify(DateTimeOffset now)
    {
        RequireStatus(VoteStatus.Closed);
        Status = VoteStatus.Ratified;
        Raise(new VoteRatifiedEvent(PublicId, Key, now));
    }

    private string ValidateChoice(string choice)
    {
        var value = (choice ?? string.Empty).Trim();
        if (string.Equals(value, AbstainChoice, StringComparison.Ordinal))
        {
            if (!AllowAbstain) throw new InvalidOperationException("Abstention is not allowed on this vote.");
            return AbstainChoice;
        }
        if (!Options.Contains(value, StringComparer.Ordinal))
            throw new InvalidOperationException($"'{value}' is not a valid option for this vote.");
        return value;
    }

    private Ballot RequireEligibleBallot(string voterSub)
    {
        RequireStatus(VoteStatus.Open);
        var ballot = _ballots.FirstOrDefault(b => string.Equals(b.VoterUserId, voterSub, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("This voter is not eligible for this vote.");
        if (ballot.Recused) throw new InvalidOperationException("This voter has been recused from this vote.");
        return ballot;
    }

    private void RequireStatus(params VoteStatus[] allowed)
    {
        if (Array.IndexOf(allowed, Status) < 0)
            throw new InvalidOperationException($"This operation is not allowed while the vote is {Status}.");
    }
}

// Input shape for seeding an eligible voter at Configure (sub + display-name snapshot). Lives in the domain
// so the factory signature is stable; the application layer maps request/roster data into it.
public sealed record VoteEligibleVoter(string UserId, string Name);
