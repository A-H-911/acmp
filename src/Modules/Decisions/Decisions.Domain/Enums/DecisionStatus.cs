namespace Acmp.Modules.Decisions.Domain.Enums;

// Decision state machine (docs/12 §W12/W21). Draft → Issued; Issued → Superseded. Issued and
// Superseded are immutable terminal-ish records: an issued decision is never edited (AC-027); a
// correction is a new decision that supersedes it (AC-028). There is no path back to Draft.
public enum DecisionStatus
{
    Draft = 0,
    Issued = 1,
    Superseded = 2,
}
