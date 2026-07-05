namespace Acmp.Modules.Risks.Domain.Enums;

// What a risk is raised against (docs/domain/domain-model.md §Risk). A soft cross-module reference — (SubjectType, SubjectId
// = the subject's PublicId) + a display-key snapshot — never an FK/navigation (ADR-0001). No existence
// check is made across the module boundary; the UI picks from real lists and the snapshot key travels.
public enum RiskSubjectType
{
    Topic = 0,
    Decision = 1,
    System = 2,
    Adr = 3,
}
