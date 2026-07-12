namespace Acmp.Modules.Research.Domain.Enums;

// A finding's confidence band (FR-113). Low/Medium/High — the evidential strength the researcher assigns to a
// finding; orthogonal to whether it has been independently verified (that is the Finding.IsVerified flag).
public enum Confidence
{
    Low = 1,
    Medium = 2,
    High = 3,
}
