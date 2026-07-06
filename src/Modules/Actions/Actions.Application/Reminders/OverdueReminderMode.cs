namespace Acmp.Modules.Actions.Application.Reminders;

// How the OWNER is re-notified while an action stays overdue (the escalation tiers are always one-shot,
// independent of this). System configuration (appsettings), default DailyWhileOverdue.
public enum OverdueReminderMode
{
    // Notify the owner exactly once, when the action first goes overdue.
    Once,

    // Notify the owner at most once per calendar day for as long as it stays overdue (the default; matches
    // the docs/domain/notification-strategy.md §3.4 "daily" job, de-duplicated so a sweep that runs more than once a day still sends once).
    DailyWhileOverdue,
}
