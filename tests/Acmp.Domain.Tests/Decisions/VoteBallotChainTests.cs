using System.Reflection;
using Acmp.Modules.Decisions.Domain;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;

namespace Acmp.Domain.Tests.Decisions;

// D-13 / C-IMM-04 (ADR-0030) — the per-ballot hash chain sealed at Close makes a direct-SQL edit, delete, or
// reorder of the frozen vote_ballots rows detectable (T-04/AB-1, the gap the vote state-change AuditEvent
// chain does not cover). Tamper is simulated with reflection on the private-set stored fields — i.e. a write
// that bypasses the aggregate, exactly like a DBA/restore-level UPDATE.
[Trait("Category", "Security")]
public class VoteBallotChainTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Topic = Guid.NewGuid();

    private static readonly VoteEligibleVoter[] ThreeVoters =
    {
        new("kc-alice", "Alice"), new("kc-bob", "Bob"), new("kc-carol", "Carol"),
    };

    // A closed vote with three cast ballots (chain sealed at Close). Ordinal voter order = alice, bob, carol.
    private static Vote ClosedVote()
    {
        var v = Vote.Configure("VOTE-2026-001", Topic, null, new[] { "Approve", "Reject" }, true,
            new QuorumRule(0, 2), ThreeVoters, "kc-sec", Now);
        v.Open("kc-sec", 3, Now);
        v.Cast("kc-alice", "Approve", null, Now);
        v.Cast("kc-bob", "Reject", null, Now);
        v.Cast("kc-carol", "Abstain", null, Now);
        v.Close("kc-sec", "Sec", Now.AddHours(1));
        return v;
    }

    private static Ballot InChainOrder(Vote v, int index) =>
        v.Ballots.OrderBy(b => b.VoterUserId, StringComparer.Ordinal).ElementAt(index);

    private static void SetPrivate(object target, string property, object? value) =>
        target.GetType()
            .GetProperty(property, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(target, value);

    [Fact]
    public void Close_seals_the_chain_and_it_verifies()
    {
        var v = ClosedVote();

        v.ChainSealedAt.Should().Be(Now.AddHours(1));
        v.Ballots.Should().OnlyContain(b => b.Hash != null && b.PreviousHash != null);
        InChainOrder(v, 0).PreviousHash.Should().Be(BallotChain.Genesis); // first ballot chains off genesis

        var result = v.VerifyBallotChain();
        result.IsValid.Should().BeTrue();
        result.IsSealed.Should().BeTrue();
        result.BrokenAtIndex.Should().BeNull();
    }

    [Fact]
    public void An_open_vote_is_unsealed_not_a_tamper()
    {
        var v = Vote.Configure("VOTE-2026-002", Topic, null, new[] { "Approve", "Reject" }, true,
            new QuorumRule(0, 1), ThreeVoters, "kc-sec", Now);
        v.Open("kc-sec", 3, Now);
        v.Cast("kc-alice", "Approve", null, Now);

        var result = v.VerifyBallotChain();
        result.IsSealed.Should().BeFalse();
        result.IsValid.Should().BeTrue(); // unsealed is not an alert
    }

    [Fact]
    public void A_legacy_closed_vote_with_no_seal_is_skipped()
    {
        var v = ClosedVote();
        SetPrivate(v, nameof(Vote.ChainSealedAt), null); // simulate a pre-P16 closed vote

        v.VerifyBallotChain().IsSealed.Should().BeFalse();
        v.VerifyBallotChain().IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_ballot_content_edit_is_detected()
    {
        var v = ClosedVote();
        SetPrivate(InChainOrder(v, 1), nameof(Ballot.Choice), "Approve"); // bob: Reject -> Approve, stale hash

        var result = v.VerifyBallotChain();
        result.IsValid.Should().BeFalse();
        result.BrokenAtIndex.Should().Be(1);
        result.Reason.Should().Contain("content tampered");
    }

    [Fact]
    public void A_broken_previous_hash_link_is_detected()
    {
        var v = ClosedVote();
        // Leave Hash intact (content check passes) but corrupt only the stored PreviousHash link on ballot 0.
        SetPrivate(InChainOrder(v, 0), nameof(Ballot.PreviousHash), new string('f', 64));

        var result = v.VerifyBallotChain();
        result.IsValid.Should().BeFalse();
        result.BrokenAtIndex.Should().Be(0);
        result.Reason.Should().Contain("chain broken");
    }

    [Fact]
    public void A_forged_tally_no_longer_matching_the_ballots_is_detected()
    {
        var v = ClosedVote();
        v.VerifyTally().Should().BeTrue(); // intact

        // Forge tally_json: claim Approve won 3-0 when the ballots say otherwise.
        SetPrivate(v, nameof(Vote.Tally), new VoteTally(
            new Dictionary<string, int> { ["Approve"] = 3, ["Reject"] = 0 }, 0, 3));

        v.VerifyTally().Should().BeFalse();
    }

    [Fact]
    public void A_never_closed_vote_has_no_tally_to_verify()
    {
        var v = Vote.Configure("VOTE-2026-003", Topic, null, new[] { "Approve", "Reject" }, true,
            new QuorumRule(0, 1), ThreeVoters, "kc-sec", Now);

        v.VerifyTally().Should().BeTrue(); // Tally null -> nothing to contradict
    }

    [Fact] // recusal is part of the hashed content: a post-seal recusal edit is caught
    public void A_recusal_edit_after_seal_is_detected()
    {
        var v = ClosedVote();
        SetPrivate(InChainOrder(v, 2), nameof(Ballot.Recused), true); // carol flipped to recused, stale hash

        v.VerifyBallotChain().IsValid.Should().BeFalse();
    }
}
