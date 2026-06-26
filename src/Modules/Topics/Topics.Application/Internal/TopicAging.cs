using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;

namespace Acmp.Modules.Topics.Application.Internal;

// Backlog aging (docs/09 §B.1, AC-057). The SLA threshold is "time in the same status"; a topic that
// has sat in its current status longer than its urgency allows is breaching SLA and earns the aging
// badge. AgeDays (since creation) drives the display column; SlaBreached drives the badge + notify.
public static class TopicAging
{
    public static int SlaThresholdDays(TopicUrgency urgency) => urgency switch
    {
        TopicUrgency.Critical => 3,
        TopicUrgency.Urgent => 7,
        _ => 21,
    };

    public static int AgeDays(DateTimeOffset createdAt, DateTimeOffset now) =>
        Math.Max(0, (int)(now - createdAt).TotalDays);

    // Terminal/decided topics no longer age (they have left the backlog).
    public static bool IsBreaching(Topic topic, DateTimeOffset now)
    {
        if (topic.Status is TopicStatus.Decided or TopicStatus.Closed or TopicStatus.Converted or TopicStatus.Rejected)
            return false;
        var statusSince = topic.History.Count > 0 ? topic.History.Max(h => h.OccurredAt) : topic.CreatedAt;
        return (now - statusSince).TotalDays > SlaThresholdDays(topic.Urgency);
    }
}
