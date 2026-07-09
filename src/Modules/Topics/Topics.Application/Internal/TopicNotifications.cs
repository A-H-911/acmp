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

    public static Func<string, NotificationMessage> TopicPrepared(string topicKey)
    {
        var title = LocalizedString.Create("Topic ready to schedule", "موضوع جاهز للجدولة");
        var body = LocalizedString.Create(
            $"Topic {topicKey} has been prepared and is ready for the agenda.",
            $"تم تجهيز الموضوع {topicKey} وأصبح جاهزًا لجدول الأعمال.");
        return recipient => new NotificationMessage(recipient, title, body, CategoryTopicPrepared, $"/topics/{topicKey}");
    }
}
