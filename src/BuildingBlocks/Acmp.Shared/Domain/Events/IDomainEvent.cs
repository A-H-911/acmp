using MediatR;

namespace Acmp.Shared.Domain.Events;

// Marker for domain events. Extends MediatR INotification so platform modules (Audit,
// Notifications, Traceability) subscribe via INotificationHandler. A module never calls those
// modules directly; it raises an event (ADR-0001, docs/34 section 12).
public interface IDomainEvent : INotification
{
    DateTimeOffset OccurredOn { get; }
}
