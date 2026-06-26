using Acmp.Shared.Domain.Entities;

namespace Acmp.Modules.Topics.Domain;

// A timestamped, attributed comment on a topic. Immutable after post (BL-033): no edit/delete surface —
// the body and author are set once at construction and never mutated.
public sealed class TopicComment : BaseEntity
{
    private TopicComment() { }

    public string Body { get; private set; } = string.Empty;
    public Guid AuthorId { get; private set; }
    public string AuthorName { get; private set; } = string.Empty;
    public DateTimeOffset PostedAt { get; private set; }

    internal TopicComment(string body, Guid authorId, string authorName, DateTimeOffset postedAt)
    {
        Body = body.Trim();
        AuthorId = authorId;
        AuthorName = authorName.Trim();
        PostedAt = postedAt;
    }
}
