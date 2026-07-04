using Acmp.Shared.Domain.Events;

namespace Acmp.Modules.Governance.Domain.Events;

// ADR lifecycle domain events (docs/12 §8, W17/W21). Raised by the aggregate on each state change; the
// application handlers own the audit + notification side-effects — the module never reaches into the Audit
// or Notifications modules (ADR-0001). Payload stays small: identity + what a subscriber needs. Draft edits
// are value mutations without a subscriber, so they carry no event (they are still audited by their handler).

public sealed record AdrDraftedEvent(Guid AdrPublicId, string Key, string AuthorUserId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record AdrProposedEvent(Guid AdrPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record AdrChangesRequestedEvent(Guid AdrPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record AdrApprovedEvent(Guid AdrPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record AdrSupersededEvent(Guid AdrPublicId, string Key, Guid SupersededByAdrId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record AdrDeprecatedEvent(Guid AdrPublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;
