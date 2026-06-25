namespace Acmp.Shared.Authorization;

// Segregation-of-duties guards (docs/10 §E.4). Pure, side-effect-free predicates the owning
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
}
