namespace Acmp.Shared.Authorization;

// Segregation-of-duties guards (docs/domain/permission-role-matrix.md §E.4). Pure, side-effect-free predicates the owning
// module's handler calls; a violation is a hard Deny regardless of role. SoD-5 (Administrator
// walled off committee content) is structural — encoded by role lists in the matrix, not here.
public static class SegregationOfDuties
{
    // SoD-1: an action's verifier must be neither its owner nor the assignee who marked it complete.
    // Enforced by the Actions module verify handler (P8); the predicate is proven here in P4.
    public static bool CanVerifyAction(string verifierId, string ownerId, string? completedById) =>
        !string.Equals(verifierId, ownerId, StringComparison.Ordinal) &&
        !string.Equals(verifierId, completedById, StringComparison.Ordinal);

    // SoD-3: the chairman cannot be the sole vote-counter — closing a vote and recording the
    // override on the same decision requires a distinct co-attester (Secretary or a second Member).
    // Enforced by the Vote close / chair-approve handlers (P9); predicate proven here in P4.
    public static bool HasIndependentCoAttestation(string chairmanId, string? coAttesterId) =>
        !string.IsNullOrWhiteSpace(coAttesterId) &&
        !string.Equals(chairmanId, coAttesterId, StringComparison.Ordinal);

    // SoD-4: a decision's RECORDER should not also be the sole owner or the presenter of the topic it
    // decides. Enforced by the Decisions record handler (WBS-26.1).
    //
    // ⚠⚠ THIS ONE IS SOFT AND ITS SIBLINGS ABOVE ARE HARD — DEC-095 d1, and the difference is deliberate
    // rather than an oversight. It returns "is this an overlap?" instead of "may this proceed?", because
    // the answer never refuses: the caller flags the decision and emits a distinct audit event, and the
    // recording is ALLOWED. A hard `recorder != owner` refusal would block a Secretary who also owns the
    // topic from recording its decision at all, which in a committee of twenty is ordinary minute-taking
    // and not self-dealing. NFR-064's own text says so, and requires each rule's strength be RECORDED —
    // SoD-1 hard, SoD-2 warn-and-audit, SoD-3 hard, SoD-4 warn-and-audit, SoD-5 hard (by non-grant).
    //
    // ⚠ THE POLARITY IS INVERTED RELATIVE TO ITS NEIGHBOURS AND THE NAME SAYS SO. CanVerifyAction and
    // HasIndependentCoAttestation return TRUE when the action is permitted; this returns TRUE when there
    // is something to warn about. Reading it as a permission check would invert the flag silently, so the
    // name is a question about the overlap, never about permission.
    //
    // Ids are CommitteeMember.PublicId on every side: the caller resolves the recorder's Keycloak subject
    // through ICommitteeDirectory before asking. A null owner or presenter is an ordinary state (a topic in
    // Triage has no owner; a decision may be recorded with no meeting) and raises no flag.
    public static bool IsRecorderConflicted(Guid recorderId, Guid? topicOwnerId, Guid? presenterId) =>
        (topicOwnerId is { } owner && owner == recorderId) ||
        (presenterId is { } presenter && presenter == recorderId);
}
