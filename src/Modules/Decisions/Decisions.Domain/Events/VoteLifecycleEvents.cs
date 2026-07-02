using Acmp.Shared.Domain.Events;

namespace Acmp.Modules.Decisions.Domain.Events;

// Vote lifecycle domain events (docs/12 §4, W11). Raised by the aggregate on each transition; the
// application handler routes them to Audit/Notifications — the Decisions module never reaches into those
// modules (ADR-0001). Payload stays small: identity + what a subscriber needs to route.

public sealed record VoteConfiguredEvent(Guid VotePublicId, string Key, Guid TopicId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record VoteOpenedEvent(Guid VotePublicId, string Key, Guid TopicId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record BallotCastEvent(Guid VotePublicId, string Key, string VoterUserId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record VoteClosedEvent(Guid VotePublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record VoteRatifiedEvent(Guid VotePublicId, string Key, DateTimeOffset OccurredOn) : IDomainEvent;
