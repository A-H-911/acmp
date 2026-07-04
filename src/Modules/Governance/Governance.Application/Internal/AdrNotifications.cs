using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Governance.Application.Internal;

// Builds the bilingual in-app notifications the Governance module raises (guardrail 9). W17: reviewers are
// told when an ADR is Proposed; stakeholders (the committee) when it is Approved. W21: the committee is
// notified when an ADR is Superseded/Deprecated. The deep link targets the routed ADR view (/adrs/{key}).
internal static class AdrNotifications
{
    public const string CategoryAdrProposed = "AdrProposed";
    public const string CategoryAdrApproved = "AdrApproved";
    public const string CategoryAdrSuperseded = "AdrSuperseded";

    private static string AdrLink(string adrKey) => $"/adrs/{adrKey}";

    // W17: a reviewer is asked to review a proposed ADR.
    public static NotificationMessage ProposedForReview(string recipientUserId, string adrKey) => new(
        recipientUserId,
        LocalizedString.Create("ADR proposed for review", "سجل قرار معماري مقترح للمراجعة"),
        LocalizedString.Create(
            $"ADR {adrKey} has been proposed and awaits your review.",
            $"تم اقتراح سجل القرار المعماري {adrKey} وينتظر مراجعتك."),
        CategoryAdrProposed, AdrLink(adrKey));

    // W17: the committee is told an ADR was approved (now in force).
    public static Func<string, NotificationMessage> Approved(string adrKey)
    {
        var title = LocalizedString.Create("ADR approved", "تم اعتماد سجل قرار معماري");
        var body = LocalizedString.Create(
            $"ADR {adrKey} has been approved and is now in force.",
            $"تم اعتماد سجل القرار المعماري {adrKey} وأصبح ساري المفعول.");
        return recipient => new NotificationMessage(recipient, title, body, CategoryAdrApproved, AdrLink(adrKey));
    }

    // W21: the committee is told an ADR was superseded/deprecated (with the replacement key when superseded).
    public static Func<string, NotificationMessage> Superseded(string priorKey, string? successorKey)
    {
        var title = LocalizedString.Create("ADR superseded", "تم استبدال سجل قرار معماري");
        var body = successorKey is null
            ? LocalizedString.Create(
                $"ADR {priorKey} has been deprecated.",
                $"تم إيقاف سجل القرار المعماري {priorKey}.")
            : LocalizedString.Create(
                $"ADR {priorKey} has been superseded by {successorKey}.",
                $"تم استبدال سجل القرار المعماري {priorKey} بالسجل {successorKey}.");
        return recipient => new NotificationMessage(recipient, title, body, CategoryAdrSuperseded, AdrLink(successorKey ?? priorKey));
    }

    // Resolve the active committee roster and deliver one notification per member (mirrors DecisionNotifications).
    public static async Task FanOutAsync(ICommitteeDirectory directory, INotificationChannel channel,
        Func<string, NotificationMessage> build, string? skipUserId, CancellationToken ct)
    {
        var members = await directory.GetActiveMembersAsync(ct);
        foreach (var member in members)
            if (!string.Equals(member.UserId, skipUserId, StringComparison.Ordinal))
                await channel.PublishAsync(build(member.UserId), ct);
    }
}
