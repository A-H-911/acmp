using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Topics.Application.Internal;

// Builds the bilingual in-app notification raised when a topic is marked Prepared (W4, AC-035). The
// Secretary roster is notified so they know an item is ready to place on an agenda. Content is
// LocalizedString (EN+AR, guardrail 9); the deep link targets the topic so the SPA navigates straight
// to it. Mirrors DecisionNotifications — the message is built once, then delivered per recipient.
internal static class TopicNotifications
{
    public const string CategoryTopicPrepared = "TopicPrepared";
    public const string CategoryTopicRejected = "TopicRejected";
    public const string CategoryTopicSlaBreach = "TopicSlaBreach";

    public static Func<string, NotificationMessage> TopicPrepared(string topicKey)
    {
        var title = LocalizedString.Create("Topic ready to schedule", "موضوع جاهز للجدولة");
        var body = LocalizedString.Create(
            $"Topic {topicKey} has been prepared and is ready for the agenda.",
            $"تم تجهيز الموضوع {topicKey} وأصبح جاهزًا لجدول الأعمال.");
        return recipient => new NotificationMessage(recipient, title, body, CategoryTopicPrepared, $"/topics/{topicKey}");
    }

    // AC-032: raised to the topic's submitter when their topic is rejected, so they learn the outcome and reason.
    public static Func<string, NotificationMessage> TopicRejected(string topicKey, string reason)
    {
        var title = LocalizedString.Create("Your topic was rejected", "تم رفض موضوعك");
        var body = LocalizedString.Create(
            $"Topic {topicKey} was rejected. Reason: {reason}",
            $"تم رفض الموضوع {topicKey}. السبب: {reason}");
        return recipient => new NotificationMessage(recipient, title, body, CategoryTopicRejected, $"/topics/{topicKey}");
    }

    // AC-057: raised to the Secretary roster when a backlog topic exceeds its urgency SLA (time-in-status).
    public static Func<string, NotificationMessage> TopicSlaBreached(string topicKey, int thresholdDays)
    {
        var title = LocalizedString.Create("Topic SLA breached", "تجاوز الموضوع مهلة الخدمة");
        var body = LocalizedString.Create(
            $"Topic {topicKey} has exceeded its {thresholdDays}-day SLA and needs attention.",
            $"تجاوز الموضوع {topicKey} مهلة الـ {thresholdDays} أيام ويحتاج إلى المتابعة.");
        return recipient => new NotificationMessage(recipient, title, body, CategoryTopicSlaBreach, $"/topics/{topicKey}");
    }
}
