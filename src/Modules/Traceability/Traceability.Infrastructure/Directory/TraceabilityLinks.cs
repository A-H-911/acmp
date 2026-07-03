using Acmp.Modules.Traceability.Application.Abstractions;
using Acmp.Modules.Traceability.Domain.Enums;
using Acmp.Shared.Contracts.Traceability;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Traceability.Infrastructure.Directory;

// Traceability-owned implementation of the shared ITraceabilityLinks port (ADR-0001): answers the Decisions
// module's widened AC-029 gate without exposing the relationships table. "Downstream" is a CURATED
// follow-through set, NOT "any edge": the decision is the source of a `recorded-as` (→ADR) or `resolves`
// (→Risk) edge, or the target of an `implements` (Action→Decision) edge. Upstream/lineage edges — `decided-by`
// (the decision's own topic), `derived-from`, `supersedes` — are deliberately EXCLUDED, so a decision can
// never satisfy the gate merely by having a topic (ASM-P10c-2). The incoming `implements` case here overlaps
// with IActionLinkDirectory's action-source check by design (an explicit edge counts even if no ActionItem
// carries the SourceId), and the two contracts are OR'd in IssueDecisionHandler.
public sealed class TraceabilityLinks : ITraceabilityLinks
{
    private readonly ITraceabilityDbContext _db;

    public TraceabilityLinks(ITraceabilityDbContext db) => _db = db;

    public Task<bool> DecisionHasDownstreamEdgeAsync(Guid decisionId, CancellationToken ct = default) =>
        _db.Relationships.AsNoTracking().AnyAsync(r => r.IsActive &&
            ((r.SourceType == ArtifactType.Decision && r.SourceId == decisionId &&
                (r.RelType == RelationshipType.RecordedAs || r.RelType == RelationshipType.Resolves))
             || (r.TargetType == ArtifactType.Decision && r.TargetId == decisionId &&
                r.RelType == RelationshipType.Implements)), ct);
}
