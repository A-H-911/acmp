using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Decisions.Application.Internal;

// Builds the bilingual in-app notification the Decisions module raises when a vote opens (W11) and fans it
// out to the eligible voters. Content is LocalizedString (EN+AR, guardrail 9). The deep link targets the
// voting UI so the SPA navigates straight to it (AC-052 / AC-021 "appears in each eligible voter's
// notification center with a deep link").
internal static class VoteNotifications
{
    public const string CategoryVoteOpened = "VoteOpened";

    private static string VoteLink(string voteKey) => $"/votes/{voteKey}";

    public static Func<string, NotificationMessage> VoteOpened(string voteKey)
    {
        var title = LocalizedString.Create("Vote opened", "تم فتح تصويت");
        var body = LocalizedString.Create(
            $"Vote {voteKey} is open. Open it to cast your ballot.",
            $"التصويت {voteKey} مفتوح الآن. افتحه للإدلاء بصوتك.");
        return recipient => new NotificationMessage(recipient, title, body, CategoryVoteOpened, VoteLink(voteKey));
    }

    // Deliver one notification per eligible voter (by Keycloak sub). Synchronous in-app writes meet the ≤5s
    // floor for a ≤20-user committee — no queue (mirrors DecisionNotifications).
    public static async Task FanOutAsync(INotificationChannel channel, IEnumerable<string> recipientSubs,
        Func<string, NotificationMessage> build, CancellationToken ct)
    {
        foreach (var sub in recipientSubs)
            await channel.PublishAsync(build(sub), ct);
    }
}
