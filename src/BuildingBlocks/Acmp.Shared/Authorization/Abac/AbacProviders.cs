namespace Acmp.Shared.Authorization.Abac;

// Ports the shared ABAC handlers call to read membership-owned facts. Implemented in
// Membership.Infrastructure (reading only its own tables) and injected here — the in-process
// public-contract pattern that keeps module boundaries intact (ADR-0001).

// The streams a principal is assigned to (docs/domain/permission-role-matrix.md §E.1).
public interface IUserStreamProvider
{
    Task<AssignedStreams> GetAssignedStreamsAsync(string userId, CancellationToken ct = default);
}

// The per-topic relationship capabilities a principal holds on a topic (docs/domain/permission-role-matrix.md §D).
public interface ITopicCapabilityResolver
{
    Task<IReadOnlyCollection<TopicCapabilityType>> GetCapabilitiesAsync(
        string userId, Guid topicId, CancellationToken ct = default);

    /// <summary>
    /// Every topic the principal holds an active capability on, in ONE call (FR-163, SC-019).
    /// </summary>
    /// <remarks>
    /// ⚠ THIS EXISTS BECAUSE THE PER-RESOURCE METHOD CANNOT BE USED FOR LIST FILTERING. Confidentiality
    /// must exclude Restricted topics from the backlog, the kanban, the agenda pool, the dashboards,
    /// the reports and search — all pages of topics. <c>GetCapabilitiesAsync</c> answers for ONE topic
    /// and issues a database round-trip per call with no caching, so asking it per row is an N+1 over
    /// every page the committee ever loads.
    /// <para>
    /// Returning ids rather than a predicate keeps the Topics module able to compose the result into a
    /// SQL-translatable <c>Where</c> without reaching across the module boundary into Membership's
    /// tables (ADR-0001) — Membership answers "which topics", Topics decides what to do with that.
    /// </para>
    /// <para>
    /// ponytail: the id set travels as an IN clause. At ≤20 users and ≤500 topics/year that is the
    /// right shape; if a member ever holds thousands of grants, move the join server-side behind a
    /// read model rather than widening this port again.
    /// </para>
    /// </remarks>
    Task<IReadOnlyCollection<Guid>> GetGrantedTopicIdsAsync(string userId, CancellationToken ct = default);
}

// Whether the principal holds an active (in-window) delegation for a capability/policy (docs/domain/permission-role-matrix.md §E.3).
public interface IDelegationResolver
{
    Task<bool> HasActiveDelegationAsync(string userId, string capability, CancellationToken ct = default);
}

// Grants/revokes a per-topic capability (Owner/Assignee/Presenter, docs/domain/permission-role-matrix.md §D). Implemented in
// Membership.Infrastructure (it owns the TopicCapabilityGrant table); called cross-module by Topics on
// accept ("grant-on-accept", W2) so the owner's per-topic relationship is resolvable by the ABAC
// CapabilityHandler. Modules never write each other's tables — this in-process port is the seam (ADR-0001).
public interface ITopicCapabilityWriter
{
    // ownerMemberId is a CommitteeMember.PublicId; Membership resolves it to the member's subject and
    // stores the grant so the ABAC CapabilityHandler (which keys on subject) can resolve it later.
    Task GrantAsync(Guid topicId, Guid ownerMemberId, TopicCapabilityType capability, CancellationToken ct = default);
}
