namespace Acmp.Modules.Risks.Domain.Enums;

// Mitigation lifecycle (docs/domain/domain-model.md §Mitigation, docs/domain/entity-lifecycles.md §10): Planned → InProgress → Done. Full mutation
// audited by the handlers.
public enum MitigationStatus
{
    Planned = 0,
    InProgress = 1,
    Done = 2,
}
