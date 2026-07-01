using Acmp.Shared.Domain.Events;

namespace Acmp.Modules.Meetings.Domain.Events;

// MinutesOfMeeting lifecycle domain events (docs/12 §6, W10). Raised by the aggregate on each transition;
// platform modules (Audit, Notifications) react via the application handler — the Meetings module never
// reaches into them (ADR-0001). Payload stays small: identity + what a subscriber needs to route.

public sealed record MinutesDraftedEvent(Guid MinutesPublicId, string Key, Guid MeetingId, int Version, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record MinutesInReviewEvent(Guid MinutesPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

// Change-request bounces the MoM back to Draft (AC-037); the author is re-notified by the handler.
public sealed record MinutesChangesRequestedEvent(Guid MinutesPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

// SoleAuthor travels on the event so audit can flag a sole-author approval (AC-014, SoD-2 soft).
public sealed record MinutesApprovedEvent(Guid MinutesPublicId, string Key, bool SoleAuthor, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record MinutesPublishedEvent(Guid MinutesPublicId, string Key, int Version, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record MinutesSupersededEvent(Guid MinutesPublicId, string Key, Guid SupersededByMinutesId, DateTimeOffset OccurredOn) : IDomainEvent;
