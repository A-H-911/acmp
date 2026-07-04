using Acmp.Shared.Domain.Events;

namespace Acmp.Modules.Governance.Domain.Events;

// Invariant lifecycle domain events (docs/12 §9, W18). Raised by the aggregate on each state change; the
// application handlers own the audit + notification side-effects — the module never reaches into the Audit or
// Notifications modules (ADR-0001). Payload stays small: identity + what a subscriber needs. Draft edits are
// value mutations without a subscriber, so they carry no event (they are still audited by their handler).

public sealed record InvariantDraftedEvent(Guid InvariantPublicId, string Key, string OwnerUserId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record InvariantProposedEvent(Guid InvariantPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record InvariantChangesRequestedEvent(Guid InvariantPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record InvariantActivatedEvent(Guid InvariantPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record InvariantRetiredEvent(Guid InvariantPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record InvariantSupersededEvent(Guid InvariantPublicId, string Key, Guid SupersededByInvariantId, DateTimeOffset OccurredOn) : IDomainEvent;
