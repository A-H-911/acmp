using Acmp.Shared.Domain.Events;

namespace Acmp.Modules.Actions.Domain.Events;

// Action lifecycle domain events (docs/12 §7). Raised by the aggregate on each transition; the
// application handlers own the audit + notification side-effects — the module never reaches into the
// Audit or Notifications modules (ADR-0001). Payload stays small: identity + what a subscriber needs.

public sealed record ActionCreatedEvent(Guid ActionPublicId, string Key, string OwnerUserId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record ActionStartedEvent(Guid ActionPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record ActionBlockedEvent(Guid ActionPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record ActionUnblockedEvent(Guid ActionPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record ActionCompletedEvent(Guid ActionPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record ActionVerifiedEvent(Guid ActionPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record ActionCancelledEvent(Guid ActionPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;
