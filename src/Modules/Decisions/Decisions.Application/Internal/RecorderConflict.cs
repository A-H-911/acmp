using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Meetings;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Topics;

namespace Acmp.Modules.Decisions.Application.Internal;

// C-AUTH-05 SoD-4 / NFR-064, WARN-AND-AUDIT by DEC-095 d1: is the subject recording a decision also the
// owner of its topic, or its presenter on the linked meeting's agenda?
//
// ⚠ IT LIVES HERE, NOT IN A HANDLER, BECAUSE TWO PATHS CREATE A DECISION. RecordDecision drafts one and
// SupersedeDecision drafts its successor, and a rule implemented in only one of them is a signal with a
// hole in it - the shape DEF-052 and DEF-056 record, where a guard existed on the path somebody thought of
// and not on its sibling. It is not in Acmp.Shared's SegregationOfDuties because that class is deliberately
// pure and side-effect-free; the PREDICATE is there, the three lookups that feed it are here.
internal static class RecorderConflict
{
    // ⚠⚠ EVERY UNKNOWN ANSWERS "NO CONFLICT", AND THAT IS THE CORRECT BIAS FOR A SOFT RULE. An actor with no
    // committee member row, a topic still in Triage with no owner, a decision recorded outside any meeting,
    // an agenda item with no presenter assigned - none is evidence of self-dealing, and a soft rule that
    // guessed "yes" would print a false warning against an honest decision and teach reviewers to ignore it.
    // ⛔ THE SAME BIAS WOULD BE WRONG IF SoD-4 WERE EVER HARDENED: a hard rule that fails open is a bypass,
    // not a lenience. Revisit this method BEFORE changing the strength, never after.
    //
    // The directory hop is not incidental - the key spaces differ. The recorder is a Keycloak subject;
    // owner and presenter are CommitteeMember.PublicId, and Decisions may read neither table (ADR-0001).
    public static async Task<bool> EvaluateAsync(
        ICommitteeDirectory committee, ITopicReader topics, IAgendaPresenterReader agenda,
        string recorderSub, Guid topicId, Guid? meetingId, CancellationToken ct)
    {
        var me = await committee.ResolveMemberAsync(recorderSub, ct);
        if (me is null) return false;

        var ownerId = await topics.GetOwnerIdAsync(topicId, ct);
        var presenterId = meetingId is { } m
            ? await agenda.GetPresenterUserIdAsync(m, topicId, ct)
            : null;

        return SegregationOfDuties.IsRecorderConflicted(me.PublicId, ownerId, presenterId);
    }

    // The audit event name, in one place so the two emitting paths cannot drift apart. A reviewer filters on
    // this alone to find every decision recorded by a conflicted actor.
    public const string AuditEvent = "Decisions.DecisionRecordedByConflictedActor";
}
