namespace Acmp.Modules.Governance.Domain.Enums;

// Architecture Invariant lifecycle (README §E, docs/domain/entity-lifecycles.md §9, docs/domain/standards-and-best-practices.md §A.5): Draft → Proposed → Active →
// (Retired | Superseded), with Proposed → Draft on requested changes. Once Active the statement is treated
// as immutable — a material change is a NEW invariant that supersedes it (ADR-0009, supersede-not-edit),
// never an edit. Violations are tracked separately (as Risk/Action/AuditEvent), never as a status here
// (docs/domain/standards-and-best-practices.md §A.5) — the violation surface (FR-108/109) is deferred (operator decision 2026-07-04).
public enum InvariantStatus
{
    Draft = 1,
    Proposed = 2,
    Active = 3,
    Retired = 4,
    Superseded = 5,
}
