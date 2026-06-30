using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Decisions.Application.Internal;

// Builds the bilingual in-app notification the Decisions module raises on issue (W12) and fans it out to
// the committee roster. Content is LocalizedString (EN+AR, guardrail 9). The deep link targets the
// decision view so the SPA navigates straight to it (AC-052 navigation shape).
internal static class DecisionNotifications
{
    public const string CategoryDecisionIssued = "DecisionIssued";

    private static string DecisionLink(string decisionKey) => $"/decisions/{decisionKey}";

    public static Func<string, NotificationMessage> DecisionIssued(string decisionKey)
    {
        var title = LocalizedString.Create("Decision issued", "تم إصدار قرار");
        var body = LocalizedString.Create(
            $"Decision {decisionKey} has been issued. Open it to review the outcome.",
            $"تم إصدار القرار {decisionKey}. افتحه لمراجعة النتيجة.");
        return recipient => new NotificationMessage(recipient, title, body, CategoryDecisionIssued, DecisionLink(decisionKey));
    }

    // Resolve the active committee roster and deliver one notification per member. Synchronous in-app
    // writes meet the ≤5s floor for a ≤20-user committee — no queue (mirrors MeetingNotifications).
    public static async Task FanOutAsync(ICommitteeDirectory directory, INotificationChannel channel,
        Func<string, NotificationMessage> build, CancellationToken ct)
    {
        var members = await directory.GetActiveMembersAsync(ct);
        foreach (var member in members)
            await channel.PublishAsync(build(member.UserId), ct);
    }
}
