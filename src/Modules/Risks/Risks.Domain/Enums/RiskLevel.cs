namespace Acmp.Modules.Risks.Domain.Enums;

// Likelihood and Impact share this ordinal 3-level scale (docs/domain/domain-model.md §Risk). Backed 1/2/3 so Severity is a
// plain product (Likelihood × Impact ∈ 1..9); the undefined default 0 makes an unset level fail IsInEnum
// at the boundary (a clean 400) and the Create guard.
public enum RiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
}
