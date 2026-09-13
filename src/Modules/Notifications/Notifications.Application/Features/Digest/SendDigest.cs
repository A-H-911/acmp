using Acmp.Modules.Notifications.Application.Abstractions;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Actions;
using Acmp.Shared.Contracts.Decisions;
using Acmp.Shared.Contracts.Meetings;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Acmp.Modules.Notifications.Application.Features.Digest;

// FR-134 / AC-161 (DEC-188): the daily and weekly notification digests. ONE handler, two Hangfire schedules
// (the worker registers both); all logic is here and unit-tested away from Hangfire, like the other sweeps.
// Opt-out is not handled here: the digest goes through the one INotificationChannel, whose in-app sink already
// withholds a category the recipient turned off (AC-160), and the shared Webex space never carries it (it is
// not in WebexEligibleEvents). No audit row: a digest is not an act.
public enum DigestPeriod { Daily, Weekly }

public sealed record SendDigestCommand(DigestPeriod Period) : IRequest<int>;

public sealed class DigestOptions
{
    public const string SectionName = "Digest";

    // Hangfire CRONs, read in TimeZone. Defaults are DOC-025 §3.7's times (07:30 daily, Monday 08:00).
    public string DailyCron { get; init; } = "30 7 * * *";
    public string WeeklyCron { get; init; } = "0 8 * * 1";

    // A system time zone id (e.g. "Asia/Riyadh"); UTC when unset (DEC-188 g3).
    public string? TimeZone { get; init; }

    // Throws on an unknown name, so a typo stops the worker at startup instead of silently running on UTC.
    public TimeZoneInfo ResolveTimeZone() =>
        string.IsNullOrWhiteSpace(TimeZone) ? TimeZoneInfo.Utc
        : TimeZoneInfo.TryFindSystemTimeZoneById(TimeZone, out var tz) ? tz
        : throw new InvalidOperationException($"Digest:TimeZone '{TimeZone}' is not a known time zone.");
}

public static class DigestNotifications
{
    public const string CategoryDailyDigest = NotificationCategories.DailyDigest;
    public const string CategoryWeeklyDigest = NotificationCategories.WeeklyDigest;

    // Counts only, never titles: the body column is 1024 characters and a count carries nothing restricted.
    public static NotificationMessage Message(string recipientUserId, DigestPeriod period, PendingActionCount actions,
        int awaitingVotes, int meetings)
    {
        var daily = period == DigestPeriod.Daily;
        return new NotificationMessage(
            recipientUserId,
            daily
                ? LocalizedString.Create("Your daily digest", "ملخصك اليومي")
                : LocalizedString.Create("Your weekly digest", "ملخصك الأسبوعي"),
            LocalizedString.Create(
                $"{actions.Pending} pending action(s), {actions.Overdue} of them overdue; {awaitingVotes} open vote(s) awaiting your ballot; {meetings} meeting(s) starting in the next {(daily ? "24 hours" : "7 days")}.",
                $"{actions.Pending} إجراء/إجراءات معلّقة، منها {actions.Overdue} متأخّرة؛ {awaitingVotes} تصويت/تصويتات مفتوحة بانتظار صوتك؛ {meetings} اجتماع/اجتماعات تبدأ خلال {(daily ? "الساعات الـ24 القادمة" : "الأيام السبعة القادمة")}."),
            daily ? CategoryDailyDigest : CategoryWeeklyDigest,
            "/");
    }
}

public sealed class SendDigestHandler : IRequestHandler<SendDigestCommand, int>
{
    private readonly INotificationsDbContext _db;
    private readonly INotificationChannel _channel;
    private readonly ICommitteeDirectory _directory;
    private readonly IActionDigestSource _actions;
    private readonly IBallotDigestSource _ballots;
    private readonly IMeetingDigestSource _meetings;
    private readonly IClock _clock;
    private readonly DigestOptions _options;

    public SendDigestHandler(INotificationsDbContext db, INotificationChannel channel, ICommitteeDirectory directory,
        IActionDigestSource actions, IBallotDigestSource ballots, IMeetingDigestSource meetings, IClock clock,
        IOptions<DigestOptions> options)
    {
        _db = db;
        _channel = channel;
        _directory = directory;
        _actions = actions;
        _ballots = ballots;
        _meetings = meetings;
        _clock = clock;
        _options = options.Value;
    }

    // Returns how many digests were handed to the channel (a member who turned the type off is withheld there).
    public async Task<int> Handle(SendDigestCommand request, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var daily = request.Period == DigestPeriod.Daily;
        var category = daily ? DigestNotifications.CategoryDailyDigest : DigestNotifications.CategoryWeeklyDigest;
        var periodStart = PeriodStart(now, _options.ResolveTimeZone(), request.Period);

        // One per member per period: a digest row already written since the period began means "already told".
        var alreadySent = (await _db.Notifications.AsNoTracking()
                .Where(n => n.Category == category && n.CreatedAt >= periodStart)
                .Select(n => n.RecipientUserId)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var meetings = await _meetings.CountStartingAsync(now, now.AddDays(daily ? 1 : 7), ct);

        // ponytail: two reads per member; the committee is <=20 people (guardrail #12). Batch them if that grows.
        var published = 0;
        foreach (var member in await _directory.GetActiveMembersAsync(ct))
        {
            if (alreadySent.Contains(member.UserId)) continue;
            var actions = await _actions.CountPendingAsync(member.UserId, now, ct);
            var votes = await _ballots.CountAwaitingAsync(member.UserId, ct);
            if (actions.Pending == 0 && votes == 0 && meetings == 0) continue;

            await _channel.PublishAsync(DigestNotifications.Message(member.UserId, request.Period, actions, votes, meetings), ct);
            published++;
        }
        return published;
    }

    // Local midnight of today (daily) or of this week's Monday (weekly), in the configured zone.
    // ponytail: in a zone whose DST change skips midnight the period starts an hour off on that one day.
    internal static DateTimeOffset PeriodStart(DateTimeOffset now, TimeZoneInfo tz, DigestPeriod period)
    {
        var day = TimeZoneInfo.ConvertTime(now, tz).Date;
        if (period == DigestPeriod.Weekly) day = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        return new DateTimeOffset(day, tz.GetUtcOffset(day));
    }
}
