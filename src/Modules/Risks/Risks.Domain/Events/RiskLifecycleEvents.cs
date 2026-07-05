using Acmp.Shared.Domain.Events;

namespace Acmp.Modules.Risks.Domain.Events;

// Risk lifecycle domain events (docs/domain/entity-lifecycles.md §10, W15). Raised by the aggregate on each state change; the
// application handlers own the audit + notification side-effects — the module never reaches into the Audit
// or Notifications modules (ADR-0001). Payload stays small: identity + what a subscriber needs. Mitigation
// add / status changes are value mutations without a subscriber, so they carry no event (they are still
// audited by their handlers) — the same choice ActionItem.UpdateProgress makes.

public sealed record RiskRaisedEvent(Guid RiskPublicId, string Key, string OwnerUserId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record RiskMitigatingEvent(Guid RiskPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record RiskClosedEvent(Guid RiskPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record RiskAcceptedEvent(Guid RiskPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record RiskEscalatedEvent(Guid RiskPublicId, string Key, string Target, DateTimeOffset OccurredOn) : IDomainEvent;
