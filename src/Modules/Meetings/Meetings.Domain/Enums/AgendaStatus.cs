namespace Acmp.Modules.Meetings.Domain.Enums;

// Agenda state machine (docs/11 §C, docs/12 §5 folded into the meeting flow). Draft → Published
// (versioned; re-publishable while still editable) → Locked (at meeting start) → Closed (meeting held).
// Items are editable only while Draft/Published; actual-time + outcome are recorded only while Locked.
public enum AgendaStatus
{
    Draft = 0,
    Published = 1,
    Locked = 2,
    Closed = 3,
}
