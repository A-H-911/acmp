using Acmp.Shared.Domain.Events;

namespace Acmp.Modules.Research.Domain.Events;

// Research mission lifecycle domain events (P15a; FR-111). Raised by the aggregate on each state change; the
// application handlers own the audit side-effects — the module never reaches into the Audit or Notifications
// modules (ADR-0001). Payload stays small: identity + what a subscriber needs. Child add/update/verify are
// value mutations without a subscriber, so they carry no event (they are still audited by their handler).

public sealed record ResearchProposedEvent(Guid MissionPublicId, string Key, string OwnerUserId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record ResearchActivatedEvent(Guid MissionPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record ResearchCompletedEvent(Guid MissionPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record ResearchCancelledEvent(Guid MissionPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;
