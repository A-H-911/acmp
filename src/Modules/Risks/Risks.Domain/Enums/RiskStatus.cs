namespace Acmp.Modules.Risks.Domain.Enums;

// Risk state machine (docs/12 §10, README §E): Open → Mitigating → Closed; side Accepted, Escalated.
// Closed and Accepted are TERMINAL. Escalated is transient — after handling it returns to Mitigating
// (resume mitigation) or Closed (docs/12 §10 line 220). Severity/Exposure are DERIVED (RiskExposure),
// never stored (docs/12 line 247). Modelled as a single enum (no orthogonal "side" column) — the same
// shape as ActionStatus folds Cancelled in as a value.
public enum RiskStatus
{
    Open = 0,
    Mitigating = 1,
    Closed = 2,
    Accepted = 3,
    Escalated = 4,
}
