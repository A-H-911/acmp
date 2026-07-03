namespace Acmp.Shared.Contracts.Traceability;

// Cross-module seam (ADR-0001): the Decisions module asks "does this decision have any DOWNSTREAM
// traceability edge?" without reading the Traceability module's tables. Implemented in
// Traceability.Infrastructure against the Relationship store (mirrors IActionLinkDirectory / ICommitteeDirectory).
// Speaks only in primitives — the Traceability RelationshipType/ArtifactType enums never leak into the shared kernel.
//
// Powers the AC-029 downstream-link gate alongside IActionLinkDirectory (P10c widens the gate: an issued
// decision is satisfied by ≥1 linked Action OR ≥1 downstream edge). "Downstream" is a CURATED semantic set,
// not merely "any edge" — the impl counts only follow-through RelTypes (decision as source of recorded-as /
// resolves, or target of implements), deliberately EXCLUDING upstream/lineage edges (decided-by, derived-from,
// supersedes) so an artifact's own topic can never satisfy the gate (ASM-P10c-2). The impl owns that logic;
// this contract stays a primitive yes/no.
public interface ITraceabilityLinks
{
    Task<bool> DecisionHasDownstreamEdgeAsync(Guid decisionId, CancellationToken ct = default);
}
