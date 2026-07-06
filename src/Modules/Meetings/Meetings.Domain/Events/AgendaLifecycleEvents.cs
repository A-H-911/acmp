using Acmp.Shared.Domain.Events;

namespace Acmp.Modules.Meetings.Domain.Events;

// Agenda lifecycle domain events (docs/domain/entity-lifecycles.md §5, W6). AgendaPublished is the trigger the application
// handler turns into (a) the Prepared→Scheduled flip of each placed topic via the Topics contract and
// (b) the in-app "agenda published" notification fan-out to committee members (AC-051).

public sealed record AgendaPublishedEvent(Guid AgendaPublicId, Guid MeetingId, string Key, int ItemCount, int Version, DateTimeOffset OccurredOn) : IDomainEvent;
