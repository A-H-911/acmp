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
