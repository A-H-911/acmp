namespace Acmp.Shared.Contracts.Meetings;

// Cross-module seam (ADR-0001): the Decisions module asks who is presenting a topic at a meeting, without
// reading Meetings' tables. Implemented in Meetings.Infrastructure over the Meetings store (mirrors
// MeetingQuorumSource, which exists for the same reason on the Vote quorum gate).
//
// WHY IT IS A NEW SEAM RATHER THAN A METHOD ON AN EXISTING ONE. Measured 2026-08-29: none of the three
// Meetings contracts exposes the presenter — IMeetingQuorumSource answers attendance, IMeetingWebexWriter
// and IWebexMeetingProvisioner are the Webex integration. Hanging a presenter lookup off a quorum port
// would make that port mean two things.
public interface IAgendaPresenterReader
{
    /// <summary>The presenter assigned to <paramref name="topicId"/> on <paramref name="meetingId"/>'s
    /// agenda, as a <c>CommitteeMember.PublicId</c> — or null when there is no such agenda item, no
    /// presenter has been assigned to it, or the meeting does not exist.</summary>
    /// <remarks>
    /// C-AUTH-05 SoD-4 / NFR-064: a decision's recorder must not be the sole owner or presenter of the
    /// topic it decides, enforced as warn-and-audit (DEC-095 d1).
    ///
    /// EVERY "NO ANSWER" COLLAPSES TO NULL DELIBERATELY, and that is safe here precisely because the rule
    /// is soft. An agenda item carries its presenter as a nullable column (Agenda.Publish refuses items
    /// with none, so an unpublished agenda legitimately has nulls), and a decision may be recorded with no
    /// meeting at all. Under a HARD rule this collapse would be dangerous — it would silently skip a
    /// refusal — so if SoD-4 is ever hardened, the distinction between "no presenter" and "unknown
    /// meeting" must be reintroduced before the strength changes, not after.
    /// </remarks>
    Task<Guid?> GetPresenterUserIdAsync(Guid meetingId, Guid topicId, CancellationToken ct = default);
}
