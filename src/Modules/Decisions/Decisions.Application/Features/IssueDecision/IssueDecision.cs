using Acmp.Modules.Decisions.Application.Abstractions;
using Acmp.Modules.Decisions.Application.Internal;
using Acmp.Modules.Decisions.Domain;
using Acmp.Modules.Decisions.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Exceptions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Actions;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Contracts.Topics;
using Acmp.Shared.Contracts.Traceability;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Decisions.Application.Features.IssueDecision;

// W12 (issue): the Chairman issues a drafted decision (Draft → Issued). RBAC = Decision.ChairApprove
// (Chairman only). On success: mark the topic Decided via the cross-module seam, fan out a DecisionIssued
// notification to the committee, and audit (payload carries the override flag, AC-016/SoD-3). A chair
// override (issuing against the vote) must carry a justification — enforced by the validator (400) and
// the domain guard (defence in depth).
//
// SoD-3 co-attestation gate (AC-015/016, P9a): when the decision is vote-coupled (VoteId set), the linked
// vote MUST exist, belong to the SAME topic, and be Closed — otherwise a missing/mismatched id would silently
// skip the gate while the decision still records as vote-coupled. The issuing chair may NOT be the vote's
// counter of record (the actor who closed it — Option A): a violation is a Forbidden (403) with the denied
// attempt audited (mirrors the SoD-1 verify handler); success also ratifies the vote (Closed → Ratified) in
// the same transaction. VoteId null → gate skipped (existing non-vote decision paths unchanged).
//
// AC-029 downstream-link gate (FR-067, OQ-045): a follow-up-bearing decision (Approved / ConditionallyApproved
// / EnhancementsRequired / DesignChangesRequired / ResearchRequired) cannot be Issued until ≥1 downstream
// artifact links to it — enforced HERE in the handler, not on Decision.Issue, because the link count is
// cross-module and the Decision domain cannot see it. Living in this handler ALSO auto-exempts the
// supersession successor (SupersedeDecisionHandler calls Decision.Issue directly, never this path) — not a
// bypass: superseding requires a prior Issued decision, and first-issue is only reachable through this gate,
// so every lineage root already passed it (ASM, docs/risks/risk-register.md). Rejected/Deferred/etc. issue freely.
//
// P10c widened "downstream link" to ANY downstream edge (ASM-P10c-2): the gate is satisfied by ≥1 linked
// ActionItem (IActionLinkDirectory) OR ≥1 downstream traceability edge (ITraceabilityLinks — decision as
// source of recorded-as/resolves, or target of implements; upstream/lineage edges excluded). A superset of
// the P8d Action-only gate, so AC-029 cannot regress. The two Shared contracts are OR'd (each module owns its
// own store; ADR-0001 forbids unifying them behind one cross-module read).
public sealed record IssueDecisionCommand(
    Guid DecisionId,
    bool ChairOverride,
    LocalizedString? OverrideJustification) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Chairman };
}

public sealed class IssueDecisionValidator : AbstractValidator<IssueDecisionCommand>
{
    public IssueDecisionValidator()
    {
        RuleFor(x => x.DecisionId).NotEmpty();

        // Override justification required (and bilingual) when overriding — clean 400, not the domain 500.
        RuleFor(x => x.OverrideJustification).NotNull().When(x => x.ChairOverride)
            .WithMessage("A chair override requires a justification.");
        RuleFor(x => x.OverrideJustification!.En).NotEmpty()
            .When(x => x.ChairOverride && x.OverrideJustification is not null)
            .WithMessage("Override justification (EN) is required.");
        RuleFor(x => x.OverrideJustification!.Ar).NotEmpty()
            .When(x => x.ChairOverride && x.OverrideJustification is not null)
            .WithMessage("Override justification (AR) is required.");
    }
}

public sealed class IssueDecisionHandler : IRequestHandler<IssueDecisionCommand>
{
    private readonly IDecisionsDbContext _db;
    private readonly ITopicDecisionRecorder _topics;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICommitteeDirectory _directory;
    private readonly INotificationChannel _notifications;
    private readonly IActionLinkDirectory _links;
    private readonly ITraceabilityLinks _traceLinks;

    public IssueDecisionHandler(IDecisionsDbContext db, ITopicDecisionRecorder topics,
        ICurrentUser user, IClock clock, IAuditSink audit,
        ICommitteeDirectory directory, INotificationChannel notifications, IActionLinkDirectory links,
        ITraceabilityLinks traceLinks)
    {
        _db = db;
        _topics = topics;
        _user = user;
        _clock = clock;
        _audit = audit;
        _directory = directory;
        _notifications = notifications;
        _links = links;
        _traceLinks = traceLinks;
    }

    public async Task Handle(IssueDecisionCommand request, CancellationToken ct)
    {
        var decision = await _db.Decisions.FirstOrDefaultAsync(d => d.PublicId == request.DecisionId, ct)
            ?? throw new KeyNotFoundException("Decision not found.");

        // AC-029: follow-up-bearing outcomes need ≥1 downstream link before Issue — a linked Action OR a
        // downstream traceability edge. Cross-module counts via the owning modules' contracts (never a
        // Decisions→Actions/Traceability table read). Short-circuits on the Action arm. InvalidOperation → 409.
        if (DecisionOutcomeRules.RequiresDownstreamLink(decision.Outcome)
            && !await _links.DecisionHasLinkedActionAsync(decision.PublicId, ct)
            && !await _traceLinks.DecisionHasDownstreamEdgeAsync(decision.PublicId, ct))
            throw new InvalidOperationException(
                "At least one downstream link (Action, Risk, or other artifact) is required before a decision can be Issued.");

        var (sub, name) = CurrentActor.Of(_user);

        // SoD-3 (AC-015/016) + vote-coupling integrity. In-module read of the Vote (same DbContext). A claimed
        // VoteId MUST resolve, match the decision's topic, and be Closed before the vote can back an issue —
        // otherwise the SoD-3 gate + ratify would be silently skipped on a dangling/mismatched reference.
        Vote? vote = null;
        if (decision.VoteId is { } voteId)
        {
            vote = await _db.Votes.FirstOrDefaultAsync(v => v.PublicId == voteId, ct)
                ?? throw new InvalidOperationException("The decision references a vote that does not exist.");
            if (vote.TopicId != decision.TopicId)
                throw new InvalidOperationException("The linked vote belongs to a different topic.");
            if (vote.Status is not (VoteStatus.Closed or VoteStatus.Ratified))
                throw new InvalidOperationException("The linked vote must be closed before the decision can be issued.");

            // SoD-3: the issuing chair may not be the vote's counter of record (the actor who closed it).
            if (!SegregationOfDuties.HasIndependentCoAttestation(sub, vote.CounterUserId))
            {
                await _audit.EmitEnrichedAsync("Decisions.DecisionIssueDenied", nameof(Decision), decision.PublicId.ToString(), AuditOutcome.Denied, ct);
                throw new ForbiddenAccessException("The chairman issuing a vote-coupled decision cannot be the vote's sole counter.");
            }
        }

        decision.Issue(sub, name, request.ChairOverride, request.OverrideJustification, _clock.UtcNow);
        vote?.Ratify(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await DecisionIssuance.ApplyAsync(_topics, _directory, _notifications, _audit,
            decision.TopicId, decision.PublicId, decision.Key, request.ChairOverride, ct);
    }
}
