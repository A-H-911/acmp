using Acmp.Modules.Risks.Domain.Enums;

namespace Acmp.Modules.Risks.Domain;

// The single source of truth for the probability×impact → exposure rule (docs/domain/reporting-dashboards.md DB-11, docs/domain/metrics-kpi-catalog.md; matches
// the design's heat-grid semantics). Severity is the plain product of the two 1..3 levels (1..9); the band
// is ≤2 Low, ≤4 Medium, ≤6 High, 9 Critical. Both the register and the detail matrix consume the projected
// band — the frontend never re-derives it (kept here so the two never drift).
public static class RiskExposureScale
{
    public static int Severity(RiskLevel likelihood, RiskLevel impact) => (int)likelihood * (int)impact;

    public static RiskExposure Band(int severity) => severity switch
    {
        <= 2 => RiskExposure.Low,
        <= 4 => RiskExposure.Medium,
        <= 6 => RiskExposure.High,
        _ => RiskExposure.Critical,
    };

    public static RiskExposure Band(RiskLevel likelihood, RiskLevel impact) => Band(Severity(likelihood, impact));
}
