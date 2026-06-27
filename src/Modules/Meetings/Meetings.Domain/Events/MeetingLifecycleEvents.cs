using Acmp.Shared.Domain.Events;

namespace Acmp.Modules.Meetings.Domain.Events;

// Meeting lifecycle domain events (docs/12 §5). Raised by the aggregate on each transition; platform
// modules (Audit, Notifications) react to them — the Meetings module never calls them directly
// (ADR-0001). Payload stays small: identity + what a subscriber needs to route. The compliance
// AuditEvent is emitted by the application handler (IAuditSink); the in-app notification fan-out is
// done by the handler too (AC-051/053).

public sealed record MeetingScheduledEvent(Guid MeetingPublicId, string Key, DateTimeOffset ScheduledStart, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record MeetingStartedEvent(Guid MeetingPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record MeetingHeldEvent(Guid MeetingPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record MeetingCancelledEvent(Guid MeetingPublicId, string Key, string Reason, DateTimeOffset OccurredOn) : IDomainEvent;
