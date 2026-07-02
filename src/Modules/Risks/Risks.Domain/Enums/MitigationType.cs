namespace Acmp.Modules.Risks.Domain.Enums;

// The response strategy a mitigation embodies (docs/11 §Mitigation). The undefined default 0 forces an
// explicit choice at the boundary (IsInEnum) and the domain guard.
public enum MitigationType
{
    Avoid = 1,
    Reduce = 2,
    Transfer = 3,
    Accept = 4,
}
