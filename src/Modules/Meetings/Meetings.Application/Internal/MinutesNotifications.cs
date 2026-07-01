using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Meetings.Application.Internal;

// Builds the bilingual in-app notifications the MoM lifecycle raises (W10). Content is LocalizedString
// (EN+AR, guardrail 9); the meeting title is user content embedded verbatim into both. The deep link
// targets the minutes tab of its meeting so the SPA navigates straight there (AC-052/AC-038 shape).
internal static class MinutesNotifications
{
    public const string CategoryMinutesPublished = "MinutesPublished";
    public const string CategoryMinutesChangesRequested = "MinutesChangesRequested";

    // Minutes live under their meeting (P7d wires the /minutes tab); the meeting key is the stable route seg.
    private static string MinutesLink(string meetingKey) => $"/meetings/{meetingKey}/minutes";

    // AC-038: on publish, every active member is notified with a deep link to the published record.
    public static Func<string, NotificationMessage> MinutesPublished(string meetingTitle, string meetingKey)
    {
        var title = LocalizedString.Create("Minutes published", "تم نشر المحضر");
        var body = LocalizedString.Create(
            $"The minutes for \"{meetingTitle}\" have been published. Open them to review the record.",
            $"تم نشر محضر \"{meetingTitle}\". افتحه لمراجعة السجل.");
        return recipient => new NotificationMessage(recipient, title, body, CategoryMinutesPublished, MinutesLink(meetingKey));
    }

    // AC-037: on a change-request, the sole author is notified so the review cycle restarts (targeted, not fanned out).
    public static NotificationMessage ChangesRequested(string authorSub, string meetingTitle, string meetingKey)
    {
        var title = LocalizedString.Create("Minutes changes requested", "طُلبت تعديلات على المحضر");
        var body = LocalizedString.Create(
            $"Changes were requested on the minutes for \"{meetingTitle}\". They are back in draft for revision.",
            $"طُلبت تعديلات على محضر \"{meetingTitle}\". أُعيد إلى المسودة للمراجعة.");
        return new NotificationMessage(authorSub, title, body, CategoryMinutesChangesRequested, MinutesLink(meetingKey));
    }

    // Resolve the active committee roster and deliver one notification per member (AC-058: disabled members
    // are excluded by the directory). Synchronous in-app writes meet the ≤5s floor for a ≤20-user committee.
    public static async Task FanOutAsync(ICommitteeDirectory directory, INotificationChannel channel,
        Func<string, NotificationMessage> build, CancellationToken ct)
    {
        var members = await directory.GetActiveMembersAsync(ct);
        foreach (var member in members)
            await channel.PublishAsync(build(member.UserId), ct);
    }
}
