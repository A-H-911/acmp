using Acmp.Modules.Knowledge.Domain.Enums;
using Acmp.Shared.Domain.Events;

namespace Acmp.Modules.Knowledge.Domain.Events;

// Knowledge lifecycle domain events (P15d; FR-116/117 Document, FR-119 Template). Raised by the aggregate on each
// state change; the application handlers own the audit side-effects — the module never reaches into the Audit or
// Notifications modules (ADR-0001). Payload stays small: identity + what a subscriber needs.

public sealed record DocumentCreatedEvent(Guid DocumentPublicId, string Key, string OwnerUserId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record DocumentEditedEvent(Guid DocumentPublicId, string Key, int Version, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record DocumentPublishedEvent(Guid DocumentPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record DocumentArchivedEvent(Guid DocumentPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record TemplateCreatedEvent(Guid TemplatePublicId, string Key, TemplateTargetType TargetType, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record TemplateEditedEvent(Guid TemplatePublicId, string Key, int Version, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record TemplateDeprecatedEvent(Guid TemplatePublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;
