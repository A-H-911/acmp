using Acmp.Shared.Contracts.Meetings;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Topics;
using NSubstitute;

namespace Acmp.Application.Tests.Decisions;

// The three ports RecordDecision and SupersedeDecision need for C-AUTH-05 SoD-4 (NFR-064), as doubles that
// answer NO CONFLICT.
//
// ⚠ THEY BELONG IN ONE PLACE ON PURPOSE. Two test classes construct these handlers, and a "no conflict"
// double copied into both is two things that can drift into disagreeing about what neutral means. The
// SoD-4 behaviour itself is proven in SegregationOfDutiesTests and RecordDecisionSoD4Tests with REAL ids;
// everything here exists so that tests about drafting, superseding, key generation and vote coupling keep
// testing what they name.
//
// ⛔ DO NOT MAKE ANY OF THESE RETURN A CONFLICT "to get more coverage". Every existing test in those classes
// would then run against a flagged decision without saying so - LL-032's shape, where the dangerous outcome
// is the pass, not the failure.
internal static class SoD4Ports
{
    // No member row for the acting subject → RecorderConflict short-circuits to false before it asks the
    // other two ports anything. Supplied alongside them rather than instead of them so a test that swaps
    // ONE port still exercises the real path.
    public static ICommitteeDirectory Committee()
    {
        var d = Substitute.For<ICommitteeDirectory>();
        d.ResolveMemberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((CommitteeMemberRef?)null);
        return d;
    }

    // A topic with no owner — the ordinary state for anything still in Triage.
    public static ITopicReader NoTopics()
    {
        var r = Substitute.For<ITopicReader>();
        r.GetOwnerIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Guid?)null);
        return r;
    }

    // No presenter assigned — the ordinary state for an unpublished agenda, or no agenda at all.
    public static IAgendaPresenterReader NoAgenda()
    {
        var a = Substitute.For<IAgendaPresenterReader>();
        a.GetPresenterUserIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Guid?)null);
        return a;
    }
}
