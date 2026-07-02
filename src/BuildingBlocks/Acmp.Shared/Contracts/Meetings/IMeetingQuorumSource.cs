namespace Acmp.Shared.Contracts.Meetings;

// Cross-module seam (ADR-0001): the Decisions module asks "how many eligible voters are present in this
// meeting?" without reading the Meetings module's tables. Implemented in Meetings.Infrastructure against the
// Meetings DbContext (mirrors how Membership implements ICommitteeDirectory and Actions implements
// IActionLinkDirectory). Speaks only in primitives — the contract never leaks Attendance/Meeting into the
// shared kernel.
//
// Powers the Vote present-quorum gate (docs/12 §4, ADR-0010): at Open, the count of eligible-and-present
// attendees must meet the vote's MinPresent threshold. "Present" = an Attendance row with IsVotingEligible
// AND Status ∈ {Present, Late} (docs/11 §Attendance — Present/Late count toward quorum).
public interface IMeetingQuorumSource
{
    Task<int> GetPresentEligibleCountAsync(Guid meetingId, CancellationToken ct = default);
}
