using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Shared.Domain.Events;

namespace Acmp.Modules.Topics.Domain.Events;

// Topic lifecycle domain events (docs/12 §1). Raised by the aggregate on each transition; platform
// modules (Audit, Notifications, Traceability) subscribe via INotificationHandler — the Topics module
// never calls them directly (ADR-0001). Payload is intentionally small: identity + what a subscriber
// needs to route. The compliance AuditEvent is emitted by the application handler (IAuditSink).

public sealed record TopicSubmittedEvent(Guid TopicPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record TopicTriagedEvent(Guid TopicPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record TopicAcceptedEvent(Guid TopicPublicId, string Key, Guid OwnerId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record TopicRejectedEvent(Guid TopicPublicId, string Key, string Reason, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record TopicDeferredEvent(Guid TopicPublicId, string Key, string Reason, DateTimeOffset? RevisitOn, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record TopicPreparedEvent(Guid TopicPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record TopicReopenedEvent(Guid TopicPublicId, string Key, string Justification, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record TopicScheduledEvent(Guid TopicPublicId, string Key, Guid MeetingId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record TopicDecidedEvent(Guid TopicPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record TopicClosedEvent(Guid TopicPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record TopicConvertedEvent(Guid TopicPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record TopicPriorityChangedEvent(Guid TopicPublicId, string Key, int Priority, DateTimeOffset OccurredOn) : IDomainEvent;
