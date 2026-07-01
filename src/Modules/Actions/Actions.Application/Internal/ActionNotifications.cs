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
}
