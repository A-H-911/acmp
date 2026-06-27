using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Shared.Domain.Entities;

namespace Acmp.Modules.Meetings.Domain;

// Captured discussion notes for one agenda topic during a meeting (docs/11 §C, W9). v1 keeps a single
// editable Human note per topic (matches the workspace's autosaved notes field); transcript-derived
// candidates (Phase 3) would be added as separate IsApproved=false rows pending human review.
// Mutated only via the Meeting root.
public sealed class Discussion : BaseEntity
{
    private Discussion() { }

    public Guid TopicId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public string AuthorSub { get; private set; } = string.Empty;
    public string AuthorName { get; private set; } = string.Empty;
    public DiscussionOrigin Origin { get; private set; }
    public bool IsApproved { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    internal Discussion(Guid topicId, string body, string authorSub, string authorName,
        DiscussionOrigin origin, bool isApproved, DateTimeOffset now)
    {
        if (topicId == Guid.Empty) throw new InvalidOperationException("A discussion must reference a topic.");
        if (string.IsNullOrWhiteSpace(body)) throw new InvalidOperationException("Discussion notes cannot be empty.");
        TopicId = topicId;
        Body = body.Trim();
        AuthorSub = authorSub.Trim();
        AuthorName = authorName.Trim();
        Origin = origin;
        IsApproved = isApproved;
        CreatedAt = now;
    }

    internal void UpdateBody(string body, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(body)) throw new InvalidOperationException("Discussion notes cannot be empty.");
        Body = body.Trim();
        UpdatedAt = now;
    }
}
