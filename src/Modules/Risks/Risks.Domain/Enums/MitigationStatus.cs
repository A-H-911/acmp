namespace Acmp.Modules.Risks.Domain.Enums;

// Mitigation lifecycle (docs/11 §Mitigation, docs/12 §10): Planned → InProgress → Done. Full mutation
// audited by the handlers.
public enum MitigationStatus
{
    Planned = 0,
    InProgress = 1,
    Done = 2,
}
