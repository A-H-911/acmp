using Acmp.Shared.Authorization;
using FluentAssertions;

namespace Acmp.Application.Tests.Authorization;

// SoD predicate mechanism (docs/10 §E.4). The end-to-end ACs land with the owning modules
// (SoD-1 -> Actions/P8, SoD-3 -> Voting/P9); these prove the guards now.
[Trait("Category", "Security")]
public class SegregationOfDutiesTests
{
    // SoD-1 (AC-012 / AC-013): an action's verifier may be neither owner nor the assignee who completed it.
    [Theory]
    [InlineData("alice", "alice", null, false)]   // verifier == owner
    [InlineData("alice", "bob", "alice", false)]  // verifier == completer
    [InlineData("bob", "alice", "alice", true)]   // independent verifier
    public void CanVerifyAction_enforces_verifier_independence(string verifier, string owner, string? completer, bool expected)
    {
        SegregationOfDuties.CanVerifyAction(verifier, owner, completer).Should().Be(expected);
    }

    // SoD-3 (AC-015 / AC-016): closing a vote + recording the chairman override needs a distinct co-attester.
    [Theory]
    [InlineData("dave", null, false)]   // no co-attester
    [InlineData("dave", "dave", false)] // chairman is the sole counter
    [InlineData("dave", "eva", true)]   // secretary co-attests
    public void Chairman_cannot_be_the_sole_vote_counter(string chairman, string? coAttester, bool expected)
    {
        SegregationOfDuties.HasIndependentCoAttestation(chairman, coAttester).Should().Be(expected);
    }

    // SoD-4 (NFR-064, WARN-AND-AUDIT per DEC-095 d1): a decision's recorder should not also be the sole
    // owner or the presenter of the topic it decides.
    //
    // ⚠ THE POLARITY IS OPPOSITE TO THE TWO ABOVE AND THE CASES ARE ORDERED TO MAKE THAT IMPOSSIBLE TO
    // MISREAD: here `true` means CONFLICTED, not permitted. A reader who skims this file and assumes the
    // house convention would invert the flag.
    [Theory]
    [InlineData(true, false, true)]    // recorder IS the topic owner
    [InlineData(false, true, true)]    // recorder IS the presenter
    [InlineData(true, true, true)]     // both, which is still one warning
    [InlineData(false, false, false)]  // a third party records - the ordinary case
    public void IsRecorderConflicted_flags_owner_or_presenter(bool isOwner, bool isPresenter, bool expected)
    {
        var recorder = Guid.NewGuid();
        var other = Guid.NewGuid();
        SegregationOfDuties.IsRecorderConflicted(
            recorder,
            isOwner ? recorder : other,
            isPresenter ? recorder : other).Should().Be(expected);
    }

    // Nulls are ORDINARY here, not error states: a topic in Triage has no owner, and a decision recorded
    // outside a meeting has no presenter. Each case pins one null against a REAL match on the other side,
    // so a bug that treated null as "matches anything" would fail rather than pass by coincidence.
    [Fact]
    public void IsRecorderConflicted_treats_a_missing_owner_or_presenter_as_no_conflict()
    {
        var recorder = Guid.NewGuid();

        SegregationOfDuties.IsRecorderConflicted(recorder, null, null).Should().BeFalse();
        SegregationOfDuties.IsRecorderConflicted(recorder, null, recorder).Should().BeTrue();
        SegregationOfDuties.IsRecorderConflicted(recorder, recorder, null).Should().BeTrue();
        SegregationOfDuties.IsRecorderConflicted(recorder, null, Guid.NewGuid()).Should().BeFalse();
    }
}
