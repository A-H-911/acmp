namespace Acmp.Shared.Authorization.Abac;

// Ports the shared ABAC handlers call to read membership-owned facts. Implemented in
// Membership.Infrastructure (reading only its own tables) and injected here — the in-process
// public-contract pattern that keeps module boundaries intact (ADR-0001).

// The streams a principal is assigned to (docs/10 §E.1).
public interface IUserStreamProvider
{
    Task<IReadOnlyCollection<string>> GetAssignedStreamsAsync(string userId, CancellationToken ct = default);
}

// The per-topic relationship capabilities a principal holds on a topic (docs/10 §D).
public interface ITopicCapabilityResolver
{
    Task<IReadOnlyCollection<TopicCapabilityType>> GetCapabilitiesAsync(
        string userId, Guid topicId, CancellationToken ct = default);
}

// Whether the principal holds an active (in-window) delegation for a capability/policy (docs/10 §E.3).
public interface IDelegationResolver
{
    Task<bool> HasActiveDelegationAsync(string userId, string capability, CancellationToken ct = default);
}
