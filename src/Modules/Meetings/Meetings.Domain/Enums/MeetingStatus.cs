namespace Acmp.Modules.Meetings.Domain.Enums;

// Meeting state machine (README §E, docs/12 §5). Scheduled → InProgress → Held; Cancelled is a side
// exit from Scheduled. Held and Cancelled are terminal: a held meeting's factual record (attendance,
// discussion) is immutable; corrections flow through MoM versioning (P7), never by reopening.
public enum MeetingStatus
{
    Scheduled = 0,
    InProgress = 1,
    Held = 2,
    Cancelled = 3,
}
