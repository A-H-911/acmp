using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Actions.Application.Internal;

// Builds the bilingual in-app notifications the Actions module raises. Unlike the Decisions committee
// fan-out, these are TARGETED to a single recipient (the owner) — assignment on create (W13) and the
// verified-and-closed notice (W14). Content is LocalizedString (EN+AR, guardrail 9); the deep link targets
// the routed action view (/actions/{key}) so the SPA navigates straight to it. Due-soon reminders and
// overdue escalation (AC-054/055) are Hangfire-driven and land in P8c.
internal static class ActionNotifications
{
    public const string CategoryActionAssigned = "ActionAssigned";
    public const string CategoryActionVerified = "ActionVerified";
    public const string CategoryActionDueReminder = "ActionDueReminder";
    public const string CategoryActionOverdue = "ActionOverdue";
    public const string CategoryActionOverdueEscalation = "ActionOverdueEscalation";

    private static string ActionLink(string actionKey) => $"/actions/{actionKey}";

    public static NotificationMessage Assigned(string recipientUserId, string actionKey) => new(
        recipientUserId,
        LocalizedString.Create("Action assigned to you", "أُسند إليك إجراء"),
        LocalizedString.Create(
            $"Action {actionKey} has been assigned to you. Open it to see the details.",
            $"تم إسناد الإجراء {actionKey} إليك. افتحه لعرض التفاصيل."),
        CategoryActionAssigned, ActionLink(actionKey));

    public static NotificationMessage Verified(string recipientUserId, string actionKey) => new(
        recipientUserId,
        LocalizedString.Create("Action verified", "تم التحقّق من الإجراء"),
        LocalizedString.Create(
            $"Action {actionKey} has been verified and closed.",
            $"تم التحقّق من الإجراء {actionKey} وإغلاقه."),
        CategoryActionVerified, ActionLink(actionKey));

    // P8c (docs/domain/notification-strategy.md §3.4 ActionDueReminder): one-shot "due soon" nudge to the owner. daysUntilDue==0 = due today.
    public static NotificationMessage DueReminder(string recipientUserId, string actionKey, int daysUntilDue) => new(
        recipientUserId,
        LocalizedString.Create("Action due soon", "إجراء يقترب موعده"),
        daysUntilDue <= 0
            ? LocalizedString.Create(
                $"Action {actionKey} is due today. Open it to complete or update it.",
                $"يستحق الإجراء {actionKey} اليوم. افتحه لإكماله أو تحديثه.")
            : LocalizedString.Create(
                $"Action {actionKey} is due in {daysUntilDue} day(s). Open it to complete or update it.",
                $"يستحق الإجراء {actionKey} خلال {daysUntilDue} يوم/أيام. افتحه لإكماله أو تحديثه."),
        CategoryActionDueReminder, ActionLink(actionKey));

    // P8c (docs/domain/notification-strategy.md §3.4 ActionOverdue): the owner's overdue notice, sent at the configured rhythm.
    public static NotificationMessage Overdue(string recipientUserId, string actionKey, int daysOverdue) => new(
        recipientUserId,
        LocalizedString.Create("Action overdue", "إجراء متأخّر"),
        LocalizedString.Create(
            $"Action {actionKey} is overdue by {daysOverdue} day(s). Please complete or update it.",
            $"تأخّر الإجراء {actionKey} بمقدار {daysOverdue} يوم/أيام. يُرجى إكماله أو تحديثه."),
        CategoryActionOverdue, ActionLink(actionKey));

    // P8c (docs/domain/notification-strategy.md §3.4 ActionOverdueEscalation): the escalation copy sent to the Secretary (>7d) / Chairman (>14d).
    public static NotificationMessage Escalation(string recipientUserId, string actionKey, int daysOverdue) => new(
        recipientUserId,
        LocalizedString.Create("Action escalation", "تصعيد إجراء متأخّر"),
        LocalizedString.Create(
            $"ESCALATION: action {actionKey} is {daysOverdue} day(s) overdue and needs attention.",
            $"تصعيد: تأخّر الإجراء {actionKey} بمقدار {daysOverdue} يوم/أيام ويحتاج إلى متابعة."),
        CategoryActionOverdueEscalation, ActionLink(actionKey));
}
