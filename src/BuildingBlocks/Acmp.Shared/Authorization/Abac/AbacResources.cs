namespace Acmp.Shared.Authorization.Abac;

// Resource contracts a module aggregate implements so the shared ABAC handlers can authorize it
// (docs/10 §E). In P4 no governance aggregate exists yet; handlers are exercised against test
// resources. P5+ Topics/Actions/etc. implement these on their entities and pass the instance to
// IAuthorizationService.AuthorizeAsync(user, resource, policy).

// The artifact belongs to a topic; ownership/relationship checks resolve against this topic id.
public interface ITopicScopedResource
{
    Guid TopicId { get; }
}

// The artifact affects one or more streams; write access is bounded by the principal's assigned
// streams unless they are committee-wide (docs/10 §E.1).
public interface IStreamScopedResource
{
    IReadOnlyCollection<string> AffectedStreams { get; }
}
