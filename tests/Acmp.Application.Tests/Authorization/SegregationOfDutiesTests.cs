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
}
