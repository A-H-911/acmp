namespace Acmp.Shared.Contracts.Membership;

// Cross-module seam (ADR-0001, shaped per ADR-0021): the committee's stream taxonomy is Membership's
// table, but TOPICS is where a submitted stream has to be validated — so the codes travel through a
// port rather than through a second module reading membership.streams.
//
// ⚠ WHY A PORT AND NOT A SHARED ENUM OR CONSTANT LIST: the taxonomy is DATA, seeded by migration and
// editable by the committee (ADR-0042 step 1). A hard-coded list in Shared would be a second source
// of truth that drifts silently the first time a stream is added, and the drift would present as a
// topic that cannot be submitted for no visible reason.
//
// Deliberately primitive (a set of codes, no entity): the contract must not reference Membership's
// Stream aggregate, and Topics has no business knowing anything about a stream beyond its identity.
public interface IStreamCatalog
{
    /// <summary>
    /// The stream codes a TOPIC may legitimately claim — every seeded stream EXCEPT the wildcard.
    /// </summary>
    /// <remarks>
    /// ⚠ The wildcard is excluded HERE, at the source, rather than by each caller filtering it out.
    /// ADR-0042 clause (4) makes the wildcard member-side only: it says "this person is unrestricted",
    /// which is meaningless as a property of a topic. Excluding it once in the catalog means a caller
    /// cannot forget to — and a caller that forgot would let a topic claim universal scope, which is
    /// the opposite of what the wildcard is for.
    ///
    /// Codes are lowercase and stable (<c>Stream.Code</c> is normalised on creation); they are the
    /// key the ABAC stream check intersects against, which is why topics must carry codes rather
    /// than display names — "Smart Cities" never matches "smart-cities" (DEF-057's third finding).
    /// </remarks>
    Task<IReadOnlyCollection<string>> GetAssignableStreamCodesAsync(CancellationToken ct = default);
}
