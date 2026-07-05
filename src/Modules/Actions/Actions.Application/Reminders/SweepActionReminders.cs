using Acmp.Modules.Actions.Application.Abstractions;
using Acmp.Modules.Actions.Application.Internal;
using Acmp.Modules.Actions.Domain;
using Acmp.Modules.Actions.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Acmp.Modules.Actions.Application.Reminders;

// W22 / docs/domain/notification-strategy.md §3.4: the recurring sweep that turns derived-overdue into notifications. HEADLESS — there is
// no ICurrentUser in a Hangfire scope, so the audit actor is the system. Idempotent: per-action "already told
// them" markers gate every send, so re-running the sweep (or running it more than once a day) never spams.
// ALL logic is here and unit-tested against a fake clock + in-memory actions; Hangfire only cron-triggers it.
public sealed record SweepActionRemindersCommand : IRequest<ActionReminderSweepResult>;

public sealed record ActionReminderSweepResult(int DueReminders, int OverdueNotices, int Escalations);

public sealed class SweepActionRemindersHandler
    : IRequestHandler<SweepActionRemindersCommand, ActionReminderSweepResult>
{
    private const string SystemActor = "system:action-reminders";

    private enum SweepKind { DueReminder, Overdue, Escalation }

    private sealed record SweepPlan(string ActionKey, List<NotificationMessage> Messages, List<SweepKind> Kinds);

    private readonly IActionsDbContext _db;
    private readonly IClock _clock;
    private readonly INotificationChannel _notifications;
    private readonly ICommitteeDirectory _directory;
    private readonly IAuditSink _audit;
    private readonly ActionReminderOptions _options;

    public SweepActionRemindersHandler(IActionsDbContext db, IClock clock, INotificationChannel notifications,
        ICommitteeDirectory directory, IAuditSink audit, IOptions<ActionReminderOptions> options)
    {
        _db = db;
        _clock = clock;
        _notifications = notifications;
        _directory = directory;
        _audit = audit;
        _options = options.Value;
    }

    public async Task<ActionReminderSweepResult> Handle(SweepActionRemindersCommand request, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var today = now.UtcDateTime.Date;

        // Only LIVE actions with a due date can need a reminder/escalation (Completed/Verified/Cancelled cannot).
        var candidates = await _db.Actions
            .Where(a => a.DueDate != null &&
                (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress || a.Status == ActionStatus.Blocked))
            .ToListAsync(ct);

        var plans = new List<SweepPlan>();

        foreach (var action in candidates)
        {
            // Everything is calendar-day math (UTC date floor), so the T-3 / >7d / >14d boundaries are stable
            // regardless of what time of day the sweep runs (avoids the classic DateTimeOffset off-by-one).
            var dueDate = action.DueDate!.Value.UtcDateTime.Date;
            var daysUntilDue = (int)(dueDate - today).TotalDays;

            var messages = new List<NotificationMessage>();
            var kinds = new List<SweepKind>();

            if (daysUntilDue >= 0)
            {
                // Not yet overdue: one-shot "due soon" reminder once we're inside the window (incl. due-today).
                if (action.DueReminderSentAt is null && daysUntilDue <= _options.DueReminderDaysBefore)
                {
                    action.MarkDueReminderSent(now);
                    messages.Add(ActionNotifications.DueReminder(action.OwnerUserId, action.Key, daysUntilDue));
                    kinds.Add(SweepKind.DueReminder);
                }
            }
            else
            {
                var daysOverdue = -daysUntilDue; // >= 1

                // Owner overdue notice, subject to the configured rhythm (Once vs DailyWhileOverdue).
                if (ShouldNotifyOverdue(action, today))
                {
                    action.MarkOverdueNotified(now);
                    messages.Add(ActionNotifications.Overdue(action.OwnerUserId, action.Key, daysOverdue));
                    kinds.Add(SweepKind.Overdue);
                }

                // Escalation tiers — one-shot each, independent of the owner-notice rhythm. Empty role list
                // (nobody currently holds it) simply sends nothing; the marker still flips so we don't re-scan it.
                if (daysOverdue > _options.EscalateToSecretaryAfterDays && action.EscalatedToSecretaryAt is null)
                {
                    action.MarkEscalatedToSecretary(now);
                    foreach (var sec in await _directory.GetActiveMembersInRoleAsync(AcmpRoles.Secretary, ct))
                    {
                        messages.Add(ActionNotifications.Escalation(sec.UserId, action.Key, daysOverdue));
                        kinds.Add(SweepKind.Escalation);
                    }
                }

                if (daysOverdue > _options.EscalateToChairmanAfterDays && action.EscalatedToChairmanAt is null)
                {
                    action.MarkEscalatedToChairman(now);
                    foreach (var chair in await _directory.GetActiveMembersInRoleAsync(AcmpRoles.Chairman, ct))
                    {
                        messages.Add(ActionNotifications.Escalation(chair.UserId, action.Key, daysOverdue));
                        kinds.Add(SweepKind.Escalation);
                    }
                }
            }

            if (kinds.Count > 0)
                plans.Add(new SweepPlan(action.Key, messages, kinds));
        }

        if (plans.Count == 0)
            return new ActionReminderSweepResult(0, 0, 0);

        // Commit the markers FIRST (idempotency), THEN send. If the save fails (e.g. a concurrent edit bumped
        // RowVersion) nothing is sent and Hangfire retries the whole sweep — the markers keep the retry from
        // double-sending. Send-after-save is deliberately at-most-once: favour "no spam" over "never miss".
        await _db.SaveChangesAsync(ct);

        foreach (var plan in plans)
        {
            foreach (var message in plan.Messages)
                await _notifications.PublishAsync(message, ct);

            await _audit.EmitAsync("Actions.RemindersSent", SystemActor,
                new { plan.ActionKey, Kinds = plan.Kinds.Select(k => k.ToString()).ToArray() }, ct);
        }

        return new ActionReminderSweepResult(
            plans.Sum(p => p.Kinds.Count(k => k == SweepKind.DueReminder)),
            plans.Sum(p => p.Kinds.Count(k => k == SweepKind.Overdue)),
            plans.Sum(p => p.Kinds.Count(k => k == SweepKind.Escalation)));
    }

    // Once = only when never notified; DailyWhileOverdue = at most once per calendar day.
    private bool ShouldNotifyOverdue(ActionItem action, DateTime today) => _options.OverdueMode switch
    {
        OverdueReminderMode.Once => action.OverdueNotifiedAt is null,
        _ => action.OverdueNotifiedAt is not { } last || last.UtcDateTime.Date < today,
    };
}
