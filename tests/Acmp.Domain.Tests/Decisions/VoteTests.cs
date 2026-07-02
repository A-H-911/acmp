using Acmp.Modules.Decisions.Domain;
using Acmp.Modules.Decisions.Domain.Enums;
using Acmp.Modules.Decisions.Domain.Events;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;

namespace Acmp.Domain.Tests.Decisions;

// W11 aggregate behaviour (docs/12 §4, ADR-0010): configure guards, the Open present-quorum gate (AC-021),
// cast + the AC-022 double-vote reject, recuse-excludes-from-quorum, the AC-024 close cast-quorum gate,
// tally freeze, and the AC-025/026 immutability (no mutators; re-transition throws; forward-only).
public class VoteTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Topic = Guid.NewGuid();

    private static readonly VoteEligibleVoter[] ThreeVoters =
    {
        new("kc-alice", "Alice"), new("kc-bob", "Bob"), new("kc-carol", "Carol"),
    };

    private static Vote Configured(int minPresent = 0, int minCast = 2, bool allowAbstain = true,
        IEnumerable<VoteEligibleVoter>? voters = null, Guid? meetingId = null) =>
        Vote.Configure("VOTE-2026-001", Topic, meetingId, new[] { "Approve", "Reject" }, allowAbstain,
            new QuorumRule(minPresent, minCast), voters ?? ThreeVoters, "kc-sec", Now);

    private static Vote Opened(int minPresent = 0, int minCast = 2, int present = 3)
    {
        var v = Configured(minPresent, minCast);
        v.Open("kc-sec", present, Now);
        return v;
    }

    [Fact]
    public void Configure_starts_Configured_seeds_awaiting_ballots_and_raises_event()
    {
        var v = Configured();

        v.Status.Should().Be(VoteStatus.Configured);
        v.TopicId.Should().Be(Topic);
        v.Options.Should().Equal("Approve", "Reject");
        v.Ballots.Should().HaveCount(3);
        v.Ballots.Should().OnlyContain(b => b.Choice == null && !b.Recused);
        v.DomainEvents.OfType<VoteConfiguredEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Configure_requires_a_topic_two_options_a_cast_quorum_and_at_least_one_voter()
    {
        var noTopic = () => Vote.Configure("K", Guid.Empty, null, new[] { "A", "B" }, false, new QuorumRule(0, 1), ThreeVoters, "s", Now);
        noTopic.Should().Throw<InvalidOperationException>().WithMessage("*topic*");

        var oneOption = () => Vote.Configure("K", Topic, null, new[] { "A" }, false, new QuorumRule(0, 1), ThreeVoters, "s", Now);
        oneOption.Should().Throw<InvalidOperationException>().WithMessage("*two options*");

        var badCast = () => Vote.Configure("K", Topic, null, new[] { "A", "B" }, false, new QuorumRule(0, 0), ThreeVoters, "s", Now);
        badCast.Should().Throw<InvalidOperationException>().WithMessage("*MinCast*");

        var noVoters = () => Vote.Configure("K", Topic, null, new[] { "A", "B" }, false, new QuorumRule(0, 1), Array.Empty<VoteEligibleVoter>(), "s", Now);
        noVoters.Should().Throw<InvalidOperationException>().WithMessage("*eligible voter*");
    }

    [Fact]
    public void Configure_dedupes_options_and_voters()
    {
        var v = Vote.Configure("VOTE-2026-002", Topic, null, new[] { "Approve", "Approve", "Reject" }, false,
            new QuorumRule(0, 1), new[] { new VoteEligibleVoter("kc-a", "A"), new VoteEligibleVoter("kc-a", "A2") }, "s", Now);

        v.Options.Should().Equal("Approve", "Reject");
        v.Ballots.Should().ContainSingle();
    }

    [Fact] // AC-021: opening locks config; present quorum must be met
    public void Open_moves_to_Open_and_enforces_present_quorum()
    {
        var tooFew = Configured(minPresent: 3);
        var act = () => tooFew.Open("kc-sec", 2, Now);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Present quorum not met*");

        var v = Configured(minPresent: 3);
        v.Open("kc-sec", 3, Now);
        v.Status.Should().Be(VoteStatus.Open);
        v.OpenedAt.Should().Be(Now);
        v.DomainEvents.OfType<VoteOpenedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Cast_records_a_choice_and_raises_event()
    {
        var v = Opened();
        v.Cast("kc-alice", "Approve", null, Now);

        var ballot = v.Ballots.Single(b => b.VoterUserId == "kc-alice");
        ballot.Choice.Should().Be("Approve");
        ballot.HasCast.Should().BeTrue();
        v.DomainEvents.OfType<BallotCastEvent>().Should().ContainSingle();
    }

    [Fact] // AC-022: a second cast is rejected; the first ballot is unchanged
    public void Cast_twice_is_rejected()
    {
        var v = Opened();
        v.Cast("kc-alice", "Approve", null, Now);

        var act = () => v.Cast("kc-alice", "Reject", null, Now);
        act.Should().Throw<InvalidOperationException>().WithMessage("*already voted*");
        v.Ballots.Single(b => b.VoterUserId == "kc-alice").Choice.Should().Be("Approve");
    }

    [Fact]
    public void Cast_rejects_an_ineligible_voter_and_an_invalid_option()
    {
        var v = Opened();
        var ineligible = () => v.Cast("kc-stranger", "Approve", null, Now);
        ineligible.Should().Throw<InvalidOperationException>().WithMessage("*not eligible*");

        var badOption = () => v.Cast("kc-alice", "Maybe", null, Now);
        badOption.Should().Throw<InvalidOperationException>().WithMessage("*not a valid option*");
    }

    [Fact]
    public void Abstain_is_allowed_only_when_configured()
    {
        var allowed = Opened();
        allowed.Cast("kc-alice", "Abstain", null, Now);
        allowed.Ballots.Single(b => b.VoterUserId == "kc-alice").Choice.Should().Be("Abstain");

        var v = Configured(allowAbstain: false);
        v.Open("kc-sec", 3, Now);
        var act = () => v.Cast("kc-alice", "Abstain", null, Now);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Abstention is not allowed*");
    }

    [Fact]
    public void ChangeBallot_overwrites_while_open()
    {
        var v = Opened();
        v.Cast("kc-alice", "Approve", null, Now);
        v.ChangeBallot("kc-alice", "Reject", null, Now);
        v.Ballots.Single(b => b.VoterUserId == "kc-alice").Choice.Should().Be("Reject");
    }

    [Fact] // recused ballots are excluded from the quorum base + tally
    public void Recuse_excludes_the_voter_from_quorum_and_tally()
    {
        var v = Opened(minCast: 2);
        v.Cast("kc-alice", "Approve", null, Now);
        v.Recuse("kc-bob", Now);

        // only 1 non-recused cast → below MinCast(2): close rejected
        var tooFew = () => v.Close("kc-sec", "Sec", Now);
        tooFew.Should().Throw<InvalidOperationException>().WithMessage("*Quorum not met*");

        v.Cast("kc-carol", "Reject", null, Now);
        v.Close("kc-sec", "Sec", Now);
        v.Tally!.CastCount.Should().Be(2);            // bob excluded
        v.Tally.OptionCounts["Approve"].Should().Be(1);
        v.Tally.OptionCounts["Reject"].Should().Be(1);
    }

    [Fact] // AC-024: close is rejected when cast quorum is not met; the vote stays Open
    public void Close_enforces_cast_quorum()
    {
        var v = Opened(minCast: 2);
        v.Cast("kc-alice", "Approve", null, Now);

        var act = () => v.Close("kc-sec", "Sec", Now);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Quorum not met: 1 of 2*");
        v.Status.Should().Be(VoteStatus.Open);
    }

    [Fact] // close freezes the tally + records the counter of record (SoD-3, Option A)
    public void Close_freezes_the_tally_and_records_the_counter()
    {
        var v = Opened(minCast: 2);
        v.Cast("kc-alice", "Approve", null, Now);
        v.Cast("kc-bob", "Approve", null, Now);
        v.Cast("kc-carol", "Abstain", null, Now);

        v.Close("kc-sec", "Sara Sec", Now.AddHours(1));

        v.Status.Should().Be(VoteStatus.Closed);
        v.ClosedAt.Should().Be(Now.AddHours(1));
        v.CounterUserId.Should().Be("kc-sec");
        v.CounterName.Should().Be("Sara Sec");
        v.Tally!.OptionCounts["Approve"].Should().Be(2);
        v.Tally.AbstainCount.Should().Be(1);
        v.Tally.CastCount.Should().Be(3);
        v.ResultSummary.Should().Contain("Approve: 2");
        v.DomainEvents.OfType<VoteClosedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Ratify_moves_Closed_to_Ratified()
    {
        var v = Opened(minCast: 1);
        v.Cast("kc-alice", "Approve", null, Now);
        v.Close("kc-sec", "Sec", Now);

        v.Ratify(Now.AddDays(1));
        v.Status.Should().Be(VoteStatus.Ratified);
        v.DomainEvents.OfType<VoteRatifiedEvent>().Should().ContainSingle();
    }

    [Fact] // AC-025/026: forward-only; re-transition throws, and there are no public mutators
    public void Vote_is_forward_only_and_immutable_after_close()
    {
        var v = Opened(minCast: 1);
        v.Cast("kc-alice", "Approve", null, Now);
        v.Close("kc-sec", "Sec", Now);

        var castAfterClose = () => v.Cast("kc-bob", "Reject", null, Now);
        castAfterClose.Should().Throw<InvalidOperationException>().WithMessage("*Closed*");
        var reOpen = () => v.Open("kc-sec", 3, Now);
        reOpen.Should().Throw<InvalidOperationException>().WithMessage("*Closed*");

        v.Ratify(Now);
        var reRatify = () => v.Ratify(Now);
        reRatify.Should().Throw<InvalidOperationException>().WithMessage("*Ratified*");
        var reClose = () => v.Close("kc-sec", "Sec", Now);
        reClose.Should().Throw<InvalidOperationException>().WithMessage("*Ratified*");

        // No public state setters on the Vote aggregate (declared members only; base EF stamps out of scope).
        typeof(Vote)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(p => p.CanWrite && p.SetMethod!.IsPublic)
            .Should().BeEmpty();
    }

    [Fact]
    public void Cannot_open_or_cast_out_of_order()
    {
        var configured = Configured();
        var castBeforeOpen = () => configured.Cast("kc-alice", "Approve", null, Now);
        castBeforeOpen.Should().Throw<InvalidOperationException>().WithMessage("*Configured*");

        var closeBeforeOpen = () => configured.Close("kc-sec", "Sec", Now);
        closeBeforeOpen.Should().Throw<InvalidOperationException>().WithMessage("*Configured*");
    }
}
