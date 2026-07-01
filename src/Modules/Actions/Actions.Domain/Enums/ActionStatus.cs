namespace Acmp.Modules.Actions.Domain.Enums;

// Action state machine (docs/12 §7, README §E): Open → InProgress ↔ Blocked → Completed → Verified.
// Cancelled is a side terminal state reachable from any non-terminal state. Overdue is DERIVED
// (DueDate < now while Open/InProgress/Blocked) — never a stored status (docs/12 line 159). Verified and
// Cancelled are terminal: there is no path back (a re-open is a NEW action, W14).
public enum ActionStatus
{
    Open = 0,
    InProgress = 1,
    Blocked = 2,
    Completed = 3,
    Verified = 4,
    Cancelled = 5,
}
