using Acmp.Modules.Topics.Domain.Enums;

namespace Acmp.Modules.Topics.Application.Internal;

// Server-side mirror of the SPA kanban grouping (src/Acmp.Web/src/features/topics/topicMeta.ts STATUS_BUCKET /
// bucketOf) — the backlog VIEW buckets over canonical TopicStatus. Used by MoveTopicPriority so a keyboard
// reorder swaps a topic within the SAME visual column the user sees. Keep in sync with topicMeta.ts.
internal static class TopicBuckets
{
    public static string BucketOf(TopicStatus status) => status switch
    {
        TopicStatus.Draft or TopicStatus.Submitted or TopicStatus.Triage or TopicStatus.Reopened => "triage",
        TopicStatus.Accepted or TopicStatus.Prepared => "accepted",
        TopicStatus.Scheduled or TopicStatus.InCommittee => "scheduled",
        TopicStatus.Deferred or TopicStatus.Rejected => "returned",
        TopicStatus.Decided or TopicStatus.Closed or TopicStatus.Converted => "done",
        _ => "triage",
    };
}
