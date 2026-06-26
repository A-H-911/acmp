using Acmp.Shared.Domain.Entities;

namespace Acmp.Modules.Topics.Domain;

// A timestamped, attributed comment on a topic. Immutable after post (BL-033): no edit/delete surface —
// the body and author are set once at construction and never mutated.
public sealed class TopicComment : BaseEntity
{
    private TopicComment() { }

    public string Body { get; private set; } = string.Empty;
    public string AuthorSub { get; private set; } = string.Empty;
    public string AuthorName { get; private set; } = string.Empty;
    public DateTimeOffset PostedAt { get; private set; }

    internal TopicComment(string body, string authorSub, string authorName, DateTimeOffset postedAt)
    {
        Body = body.Trim();
        AuthorSub = authorSub.Trim();
        AuthorName = authorName.Trim();
        PostedAt = postedAt;
    }
}
