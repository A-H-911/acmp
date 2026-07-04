using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Governance.Application.Internal;

// Builds the bilingual in-app notifications the Invariant flow raises (guardrail 9). W18: reviewers are told
// when an invariant is Proposed; the committee when it is Activated (now in force). W21: the committee is
// notified when an invariant is Superseded/Retired. The deep link targets the routed invariant view
// (/invariants/{key}). NOTE: docs/12 §9 says "stream owners on Activate", but an invariant's scope is a class
// (single/multi/platform/org-wide), not a link to a specific stream — there is no stream roster to resolve —
// so activation notifies the committee (P11d flag: add a stream link if per-stream targeting is ever wanted).
internal static class InvariantNotifications
{
    public const string CategoryInvariantProposed = "InvariantProposed";
    public const string CategoryInvariantActivated = "InvariantActivated";
    public const string CategoryInvariantSuperseded = "InvariantSuperseded";

    private static string InvariantLink(string key) => $"/invariants/{key}";

    // W18: a reviewer is asked to review a proposed invariant.
    public static NotificationMessage ProposedForReview(string recipientUserId, string key) => new(
        recipientUserId,
        LocalizedString.Create("Invariant proposed for review", "ثابت معماري مقترح للمراجعة"),
        LocalizedString.Create(
            $"Invariant {key} has been proposed and awaits your review.",
            $"تم اقتراح الثابت المعماري {key} وينتظر مراجعتك."),
        CategoryInvariantProposed, InvariantLink(key));

    // W18: the committee is told an invariant was activated (now in force).
    public static Func<string, NotificationMessage> Activated(string key)
    {
        var title = LocalizedString.Create("Invariant activated", "تم تفعيل ثابت معماري");
        var body = LocalizedString.Create(
            $"Invariant {key} is now active and in force.",
            $"أصبح الثابت المعماري {key} فعّالاً وساري المفعول.");
        return recipient => new NotificationMessage(recipient, title, body, CategoryInvariantActivated, InvariantLink(key));
    }

    // W21: the committee is told an invariant was superseded/retired (with the replacement key when superseded).
    public static Func<string, NotificationMessage> Superseded(string priorKey, string? successorKey)
    {
        var title = LocalizedString.Create("Invariant superseded", "تم استبدال ثابت معماري");
        var body = successorKey is null
            ? LocalizedString.Create(
                $"Invariant {priorKey} has been retired.",
                $"تم سحب الثابت المعماري {priorKey}.")
            : LocalizedString.Create(
                $"Invariant {priorKey} has been superseded by {successorKey}.",
                $"تم استبدال الثابت المعماري {priorKey} بالثابت {successorKey}.");
        return recipient => new NotificationMessage(recipient, title, body, CategoryInvariantSuperseded, InvariantLink(successorKey ?? priorKey));
    }

    // Resolve the active committee roster and deliver one notification per member (mirrors AdrNotifications).
    public static async Task FanOutAsync(ICommitteeDirectory directory, INotificationChannel channel,
        Func<string, NotificationMessage> build, string? skipUserId, CancellationToken ct)
    {
        var members = await directory.GetActiveMembersAsync(ct);
        foreach (var member in members)
            if (!string.Equals(member.UserId, skipUserId, StringComparison.Ordinal))
                await channel.PublishAsync(build(member.UserId), ct);
    }
}
