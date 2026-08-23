namespace Acmp.Shared.Authorization.Abac;

// Resource contracts a module aggregate implements so the shared ABAC handlers can authorize it
// (docs/domain/permission-role-matrix.md §E). In P4 no governance aggregate exists yet; handlers are exercised against test
// resources. P5+ Topics/Actions/etc. implement these on their entities and pass the instance to
// IAuthorizationService.AuthorizeAsync(user, resource, policy).

// The artifact belongs to a topic; ownership/relationship checks resolve against this topic id.
public interface ITopicScopedResource
{
    Guid TopicId { get; }
}

// The artifact affects one or more streams; write access is bounded by the principal's assigned
// streams unless they are committee-wide (docs/domain/permission-role-matrix.md §E.1).
public interface IStreamScopedResource
{
    IReadOnlyCollection<string> AffectedStreams { get; }

    /// <summary>
    /// True when this artifact affects the whole committee, so stream scope does not bound it
    /// (ADR-0043 clause 5). An implementer must DECLARE this — it is never inferred.
    /// </summary>
    /// <remarks>
    /// ⚠ IT IS A PRIMITIVE bool AND MUST STAY ONE. Topic computes it from TopicScope, which lives in
    /// Topics.Domain, while this contract lives in the shared kernel — referencing the module's enum
    /// here would invert the dependency ADR-0001 draws and ADR-0021 exists to keep drawn.
    /// <para>
    /// ⚠ IT REPLACES AN INFERENCE, AND THAT IS THE POINT (DEF-059, DEC-043). StreamScopeHandler used
    /// to read an EMPTY AffectedStreams as "unscoped" and grant universally. Topic only enforces a
    /// non-empty set at Submit, so an update could empty a live topic and hand every stream-bounded
    /// member write access to it — and worse, every FUTURE implementer of this contract (Actions,
    /// Risks, ADRs) would have inherited that universal grant without its author ever seeing the
    /// line. An empty set now means "affects nothing anyone holds", which denies.
    /// </para>
    /// </remarks>
    bool AffectsAllStreams { get; }
}

// C-AUTHZ-04 / FR-163: the artifact carries a confidentiality classification. A Restricted artifact is
// readable only by the committee-wide readers named in DEC-063 d1 (Chairman, Secretary, Auditor), its
// Owner, and holders of an explicit per-topic capability grant.
public interface IConfidentialResource
{
    /// <summary>
    /// True when this artifact is classified Restricted and its visibility is narrowed accordingly.
    /// </summary>
    /// <remarks>
    /// ⚠ A PRIMITIVE bool, and it must stay one — the same reason AffectsAllStreams is (ADR-0001 /
    /// ADR-0021): a Confidentiality enum would live in Topics.Domain, and referencing it from the
    /// shared kernel would invert the dependency those ADRs exist to keep drawn.
    /// <para>
    /// ⚠ CONFIDENTIALITY NARROWS, NEVER WIDENS (SEC-304). False must mean "apply the normal rules",
    /// never "grant" — so every consumer reads this as an additional restriction on top of whatever
    /// access the principal already had, and never as a reason to allow something otherwise refused.
    /// </para>
    /// </remarks>
    bool IsRestricted { get; }
}
