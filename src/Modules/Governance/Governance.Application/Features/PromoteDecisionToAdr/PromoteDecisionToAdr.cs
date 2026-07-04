using Acmp.Modules.Governance.Application.Abstractions;
using Acmp.Modules.Governance.Application.Contracts;
using Acmp.Modules.Governance.Application.Internal;
using Acmp.Modules.Governance.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Decisions;
using Acmp.Shared.Contracts.Traceability;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Governance.Application.Features.PromoteDecisionToAdr;

// FR-068 / W17: the Chairman promotes an ISSUED committee decision to a new ADR, pre-filled from the decision
// and bidirectionally linked. RBAC = Adr.Promote (Chairman only — stricter than Adr.Create). The ADR is born
// Draft; the Chairman refines it and runs the normal ADR lifecycle. Blocked (409) when the decision is not
// Issued or has already been promoted (one ADR per decision). Bidirectional link = SourceDecisionId on the ADR
// (ADR → Decision) plus a RecordedAs traceability edge (Decision → ADR) so both detail panels cross-link.
public sealed record PromoteDecisionToAdrCommand(Guid DecisionId) : IRequest<AdrSummaryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Chairman };
}

public sealed class PromoteDecisionToAdrValidator : AbstractValidator<PromoteDecisionToAdrCommand>
{
    public PromoteDecisionToAdrValidator()
        => RuleFor(x => x.DecisionId).NotEmpty().WithMessage("A decision is required.");
}

public sealed class PromoteDecisionToAdrHandler : IRequestHandler<PromoteDecisionToAdrCommand, AdrSummaryDto>
{
    private const string IssuedStatus = "Issued";

    private readonly IGovernanceDbContext _db;
    private readonly IAdrKeyGenerator _keys;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly IDecisionReader _decisions;
    private readonly ITraceabilityWriter _trace;

    public PromoteDecisionToAdrHandler(IGovernanceDbContext db, IAdrKeyGenerator keys, ICurrentUser user,
        IClock clock, IAuditSink audit, IDecisionReader decisions, ITraceabilityWriter trace)
    {
        _db = db;
        _keys = keys;
        _user = user;
        _clock = clock;
        _audit = audit;
        _decisions = decisions;
        _trace = trace;
    }

    public async Task<AdrSummaryDto> Handle(PromoteDecisionToAdrCommand request, CancellationToken ct)
    {
        var decision = await _decisions.GetForPromotionAsync(request.DecisionId, ct)
            ?? throw new KeyNotFoundException("Decision not found.");

        if (!string.Equals(decision.Status, IssuedStatus, StringComparison.Ordinal))
            throw new InvalidOperationException("Only an issued decision can be promoted to an ADR.");

        // One ADR per decision — a second promotion is blocked (409) and names the existing ADR. The retry also
        // HEALS a missing reverse edge: the ADR insert and the Decision→ADR edge commit in two transactions
        // (separate DbContexts — no ambient transaction to avoid MSDTC on-prem), so a crash between them can
        // leave the ADR without its edge. Re-running Convert re-records the edge idempotently, then reports 409.
        // A same-instant concurrent double-promote that races past this app guard is caught by the filtered
        // unique index on SourceDecisionId (AdrConfiguration) — the DB rejects the second insert, never a dup ADR.
        var existing = await _db.Adrs.AsNoTracking().FirstOrDefaultAsync(a => a.SourceDecisionId == decision.Id, ct);
        if (existing is not null)
        {
            await _trace.RecordEdgeAsync(
                "Decision", decision.Id, decision.Key, decision.Title.En,
                "Adr", existing.PublicId, existing.Key, existing.Title.En,
                relTypeName: "RecordedAs", ct);
            throw new InvalidOperationException($"This decision has already been promoted to ADR {existing.Key}.");
        }

        var now = _clock.UtcNow;
        var (sub, name) = CurrentActor.Of(_user);
        var key = await _keys.NextAdrKeyAsync(now.Year, ct);

        // Pre-fill mapping (decision → MADR-lite ADR; both already bilingual, so no mirroring is needed):
        //   Title ← Title, Context ← Rationale (the why/forces), Decision ← Statement (what was decided),
        //   Drivers ← Alternatives (considered options). Consequences/options are left empty — the Chairman
        //   refines the Draft before proposing. Context + Decision are the ADR's required sections; Rationale
        //   and Statement are required on the decision, so a valid Draft is always produced.
        var adr = Adr.Draft(key, decision.Title, decision.Rationale, decision.Alternatives, decision.Statement,
            consequencesPositive: null, consequencesNegative: null, options: Array.Empty<AdrOptionInput>(),
            sub, name, sourceDecisionId: decision.Id, now);

        _db.Adrs.Add(adr);
        await _db.SaveChangesAsync(ct);

        // Bidirectional link: a RecordedAs edge (Decision → ADR) so both traceability panels cross-link. Title
        // snapshots use EN (the stored snapshot is a single string; the SPA localizes labels, not snapshots).
        await _trace.RecordEdgeAsync(
            "Decision", decision.Id, decision.Key, decision.Title.En,
            "Adr", adr.PublicId, adr.Key, adr.Title.En,
            relTypeName: "RecordedAs", ct);

        await _audit.EmitAsync("Governance.AdrPromotedFromDecision", sub,
            new { adr.PublicId, adr.Key, DecisionId = decision.Id, DecisionKey = decision.Key }, ct);

        return AdrMapping.ToSummary(adr);
    }
}
