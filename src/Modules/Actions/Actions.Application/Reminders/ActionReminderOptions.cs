namespace Acmp.Modules.Actions.Application.Reminders;

// System-configuration knobs for the Hangfire reminder/escalation sweep (docs/29 §3.4, W22). Bound from
// appsettings "ActionReminders" via the Options pattern the host already uses. Right-sized (guardrail #12,
// ≤20 users): thresholds + rhythm + cron only — no live-editable admin store.
public sealed class ActionReminderOptions
{
    public const string SectionName = "ActionReminders";

    // Days before the due date the owner gets the one-shot "due soon" reminder (docs/29 ActionDueReminder = 3).
    public int DueReminderDaysBefore { get; init; } = 3;

    // Days overdue after which the Secretary is copied on the escalation (docs/29 = 7).
    public int EscalateToSecretaryAfterDays { get; init; } = 7;

    // Days overdue after which the Chairman is also copied (docs/29 = 14).
    public int EscalateToChairmanAfterDays { get; init; } = 14;

    // How often the owner is re-notified while an action stays overdue (operator call; default DailyWhileOverdue).
    public OverdueReminderMode OverdueMode { get; init; } = OverdueReminderMode.DailyWhileOverdue;

    // Hangfire CRON for the sweep. Default: every day at 06:00 server time (docs/29 = daily job).
    public string SweepCron { get; init; } = "0 6 * * *";
}
