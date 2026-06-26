using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Shared.Domain.Entities;

namespace Acmp.Modules.Topics.Domain;

// An append-only record of a topic status transition (the detail "History" tab). The rejection/defer
// reason is captured here and is immutable (AC-032, AC-033): rows are only ever appended, never edited
// or deleted. This is the per-topic history; the immutable hash-chained AuditEvent log is a separate
// platform concern (BL-066).
public sealed class TopicStatusEvent : BaseEntity
{
    private TopicStatusEvent() { }

    public TopicStatus FromStatus { get; private set; }
    public TopicStatus ToStatus { get; private set; }
    public string? Reason { get; private set; }
    public string ActorSub { get; private set; } = string.Empty;
    public string ActorName { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }

    internal TopicStatusEvent(TopicStatus from, TopicStatus to, string? reason,
        string actorSub, string actorName, DateTimeOffset occurredAt)
    {
        FromStatus = from;
        ToStatus = to;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        ActorSub = actorSub.Trim();
        ActorName = actorName.Trim();
        OccurredAt = occurredAt;
    }
}
