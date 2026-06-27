using System.Globalization;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Meetings.Application.Internal;

// Builds the bilingual in-app notifications the Meetings module raises (P6b: meeting scheduled, agenda
// published) and fans them out to the committee roster. Content is LocalizedString (EN+AR, guardrail 9);
// the meeting title is user content embedded verbatim into both. The deep link is a structured field so
// the SPA's notification center navigates straight to the target (AC-052 navigation shape).
// ponytail: the date is embedded as an invariant Gregorian yyyy-MM-dd — the SPA may reformat per locale,
// but a stable Gregorian string in the body is honest and locale-safe.
internal static class MeetingNotifications
{
    public const string CategoryMeetingScheduled = "MeetingScheduled";
    public const string CategoryAgendaPublished = "AgendaPublished";

    private static string MeetingLink(string meetingKey) => $"/meetings/{meetingKey}";

    private static string Date(DateTimeOffset start) => start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // No AC content contract behind this one (phase-scope notification) — a sensible bilingual heads-up.
    public static Func<string, NotificationMessage> MeetingScheduled(string title, string meetingKey, DateTimeOffset start)
    {
        var date = Date(start);
        var t = LocalizedString.Create("Meeting scheduled", "تم جدولة اجتماع");
        var body = LocalizedString.Create(
            $"\"{title}\" is scheduled for {date}.",
            $"تمت جدولة \"{title}\" بتاريخ {date}.");
        return recipient => new NotificationMessage(recipient, t, body, CategoryMeetingScheduled, MeetingLink(meetingKey));
    }

    // AC-051: the body carries the meeting date and the agenda title, and DeepLink targets the agenda view.
    public static Func<string, NotificationMessage> AgendaPublished(string title, string meetingKey, DateTimeOffset start)
    {
        var date = Date(start);
        var t = LocalizedString.Create("Agenda published", "تم نشر جدول الأعمال");
        var body = LocalizedString.Create(
            $"The agenda for \"{title}\" on {date} has been published. Open it to review the topics.",
            $"تم نشر جدول أعمال \"{title}\" بتاريخ {date}. افتحه لمراجعة المواضيع.");
        return recipient => new NotificationMessage(recipient, t, body, CategoryAgendaPublished, MeetingLink(meetingKey));
    }

    // Resolve the active committee roster and deliver one notification per member (AC-051 "every committee
    // member"; AC-058 disabled members are excluded by the directory). Synchronous in-app writes meet the
    // ≤5s floor for a ≤20-user committee — no queue.
    public static async Task FanOutAsync(ICommitteeDirectory directory, INotificationChannel channel,
        Func<string, NotificationMessage> build, CancellationToken ct)
    {
        var members = await directory.GetActiveMembersAsync(ct);
        foreach (var member in members)
            await channel.PublishAsync(build(member.UserId), ct);
    }
}
