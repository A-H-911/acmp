using Acmp.Modules.Decisions.Application.Abstractions;
using Acmp.Modules.Decisions.Application.Contracts;
using Acmp.Modules.Decisions.Application.Internal;
using Acmp.Modules.Decisions.Domain;
using Acmp.Modules.Decisions.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Decisions.Application.Features.RecordDecision;

// W12 (record): the Secretary (or Chairman) drafts a decision against a topic. Creates a Draft — not yet
// issued, so it is still mutable-by-replacement and carries no chair attribution. RBAC = Decision.Record.
// AC-029 (a downstream Action/Risk link required before a decision can be ISSUED) is now enforced at issue
// time (P8d, IssueDecisionHandler via IActionLinkDirectory). A supplied VoteId is validated here (exists +
// same topic) so a dangling/mismatched vote-coupling is never persisted (defence in depth for SoD-3).
public sealed record RecordDecisionCommand(
    Guid TopicId,
    Guid? MeetingId,
    DecisionOutcome Outcome,
    LocalizedString Title,
    LocalizedString Statement,
    LocalizedString Rationale,
    LocalizedString? Alternatives,
    Guid? VoteId,
    IReadOnlyList<DecisionConditionRequest> Conditions) : IRequest<DecisionSummaryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Secretary, AcmpRoles.Chairman };
}

public sealed class RecordDecisionValidator : AbstractValidator<RecordDecisionCommand>
{
    public RecordDecisionValidator()
    {
        RuleFor(x => x.TopicId).NotEmpty().WithMessage("A topic is required.");
        RuleFor(x => x.Outcome).IsInEnum();

        // LocalizedString's positional ctor does NOT validate (only Create does, and that throws an
        // ArgumentException → 500). So the boundary check lives here to produce a clean 400 (docs/domain/data-architecture.md §1.5).
        // Content is entered in one UI language and MIRRORED to both columns (the operator's choice — keeps
        // both columns populated for Full-Text Search), so both EN and AR must be present. The per-language
        // max guards the nvarchar(512) title column so an over-long title is a clean 400, not a SaveChanges 500.
        RuleFor(x => x.Title).NotNull().WithMessage("A title is required.");
        RuleFor(x => x.Title!.En).NotEmpty().MaximumLength(512).When(x => x.Title is not null).WithMessage("Title (EN) is required (max 512).");
        RuleFor(x => x.Title!.Ar).NotEmpty().MaximumLength(512).When(x => x.Title is not null).WithMessage("Title (AR) is required (max 512).");

        RuleFor(x => x.Statement).NotNull().WithMessage("A decision statement is required.");
        RuleFor(x => x.Statement!.En).NotEmpty().MaximumLength(2000).When(x => x.Statement is not null).WithMessage("Statement (EN) is required (max 2000).");
        RuleFor(x => x.Statement!.Ar).NotEmpty().MaximumLength(2000).When(x => x.Statement is not null).WithMessage("Statement (AR) is required (max 2000).");

        RuleFor(x => x.Rationale).NotNull().WithMessage("A rationale is required.");
        RuleFor(x => x.Rationale!.En).NotEmpty().When(x => x.Rationale is not null).WithMessage("Rationale (EN) is required.");
        RuleFor(x => x.Rationale!.Ar).NotEmpty().When(x => x.Rationale is not null).WithMessage("Rationale (AR) is required.");

        // ConditionallyApproved must carry ≥1 condition (matches the domain guard; caught earlier as 400).
        RuleFor(x => x.Conditions)
            .Must(c => c is { Count: > 0 })
            .When(x => x.Outcome == DecisionOutcome.ConditionallyApproved)
            .WithMessage("A conditionally-approved decision requires at least one condition.");

        // Each condition's bilingual text is validated here too — otherwise a null/empty Text would reach
        // the domain ctor and throw (→ 409) instead of a clean field-level 400 (same reason as Rationale).
        RuleForEach(x => x.Conditions).ChildRules(c =>
        {
            c.RuleFor(r => r.Text).NotNull().WithMessage("A condition requires text.");
            c.RuleFor(r => r.Text!.En).NotEmpty().When(r => r.Text is not null).WithMessage("Condition text (EN) is required.");
            c.RuleFor(r => r.Text!.Ar).NotEmpty().When(r => r.Text is not null).WithMessage("Condition text (AR) is required.");
        });
    }
}

public sealed class RecordDecisionHandler : IRequestHandler<RecordDecisionCommand, DecisionSummaryDto>
{
    private readonly IDecisionsDbContext _db;
    private readonly IDecisionKeyGenerator _keys;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;

    public RecordDecisionHandler(IDecisionsDbContext db, IDecisionKeyGenerator keys,
        ICurrentUser user, IClock clock, IAuditSink audit)
    {
        _db = db;
        _keys = keys;
        _user = user;
        _clock = clock;
        _audit = audit;
    }

    public async Task<DecisionSummaryDto> Handle(RecordDecisionCommand request, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var (sub, _) = CurrentActor.Of(_user);

        // A vote-coupled draft must reference an existing vote on the SAME topic — reject a dangling or
        // cross-topic coupling at draft time (the issue path re-checks + gates SoD-3). In-module read.
        if (request.VoteId is { } voteId)
        {
            var vote = await _db.Votes.AsNoTracking().FirstOrDefaultAsync(v => v.PublicId == voteId, ct)
                ?? throw new InvalidOperationException("The referenced vote does not exist.");
            if (vote.TopicId != request.TopicId)
                throw new InvalidOperationException("The referenced vote belongs to a different topic.");
        }

        var key = await _keys.NextDecisionKeyAsync(now.Year, ct);
        var conditions = (request.Conditions ?? Array.Empty<DecisionConditionRequest>())
            .Select(c => new DecisionConditionInput(c.Text, c.DueDate));

        var decision = Decision.Draft(key, request.TopicId, request.MeetingId, request.Outcome,
            request.Title, request.Statement, request.Rationale, request.Alternatives, request.VoteId, conditions, sub, now);

        _db.Decisions.Add(decision);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitAsync("Decisions.DecisionDrafted", sub, new { decision.PublicId, decision.Key }, ct);

        return DecisionMapping.ToSummary(decision);
    }
}
