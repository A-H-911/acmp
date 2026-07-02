namespace Acmp.Modules.Risks.Domain.Enums;

// The derived exposure band shown on the register heat grid + detail matrix (the design's "Exposure"
// column / expSem). Computed from Severity = Likelihood × Impact (docs/27 DB-11, docs/28): ≤2 Low,
// ≤4 Medium, ≤6 High, 9 Critical. Never stored — projected into the read models (docs/12 line 247).
// (docs/11 types the numeric score as `Severity:int`; the KPI/dashboard "Critical/High/…" band is this.)
public enum RiskExposure
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3,
}
