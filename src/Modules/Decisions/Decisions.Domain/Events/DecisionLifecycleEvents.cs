using Acmp.Shared.Domain.Events;

namespace Acmp.Modules.Decisions.Domain.Events;

// Decision lifecycle domain events (docs/domain/entity-lifecycles.md §W12/W21). Raised by the aggregate on each transition;
// platform modules (Audit, Notifications) react via the application handler — the Decisions module never
// reaches into them (ADR-0001). Payload stays small: identity + what a subscriber needs to route.

public sealed record DecisionDraftedEvent(Guid DecisionPublicId, string Key, Guid TopicId, DateTimeOffset OccurredOn) : IDomainEvent;

// ChairOverride travels on the event so audit/notification can flag a chair-override issuance (AC-016, SoD-3).
public sealed record DecisionIssuedEvent(Guid DecisionPublicId, string Key, Guid TopicId, bool ChairOverride, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record DecisionSupersededEvent(Guid DecisionPublicId, string Key, Guid SupersededByDecisionId, DateTimeOffset OccurredOn) : IDomainEvent;
