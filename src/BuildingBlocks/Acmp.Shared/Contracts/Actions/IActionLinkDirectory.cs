namespace Acmp.Shared.Contracts.Actions;

// Cross-module seam (ADR-0001): the Decisions module asks "does this decision drive any follow-through?"
// without reading the Actions module's tables. Implemented in Actions.Infrastructure against the Actions
// DbContext (mirrors how Membership implements ICommitteeDirectory). Speaks only in primitives — the
// contract never leaks ActionItem/ActionSourceType into the shared kernel; the impl maps decisionId to the
// (SourceType=Decision, SourceId) soft reference internally.
//
// Powers the AC-029 downstream-link gate (FR-067, OQ-045): a follow-up-bearing decision cannot be Issued
// until ≥1 downstream artifact links to it. Until the Risk module (P10) and the typed-edge Traceability
// module (ADR-0008) land, "downstream link" == "≥1 ActionItem sourced from the decision" (ASM, docs/41).
public interface IActionLinkDirectory
{
    Task<bool> DecisionHasLinkedActionAsync(Guid decisionId, CancellationToken ct = default);
}
