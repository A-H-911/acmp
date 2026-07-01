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

namespace Acmp.Modules.Decisions.Application.Features.RecordDecision;

// W12 (record): the Secretary (or Chairman) drafts a decision against a topic. Creates a Draft — not yet
// issued, so it is still mutable-by-replacement and carries no chair attribution. RBAC = Decision.Record.
// AC-029 (a downstream Action/Risk link required before a decision can be ISSUED) is DEFERRED to P8
// (OQ-045) — there is no Action artifact yet, so the gate is unbuildable and intentionally NOT enforced.
public sealed record RecordDecisionCommand(
    Guid TopicId,
    Guid? MeetingId,
    DecisionOutcome Outcome,
    LocalizedString Title,
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
        // ArgumentException → 500). So the boundary check lives here to produce a clean 400 (docs/16 §1.5).
        // Content is entered in ONE language (the operator's choice — the other column stays empty; reads
        // fall back), so the rule is "at least one language present", not "both". The per-language max still
        // guards the nvarchar(512) title column so an over-long title is a clean 400, not a SaveChanges 500.
        RuleFor(x => x.Title).NotNull().WithMessage("A title is required.");
        RuleFor(x => x.Title!).Must(DecisionText.HasEitherLanguage).When(x => x.Title is not null).WithMessage("A title is required in at least one language.");
        RuleFor(x => x.Title!.En).MaximumLength(512).When(x => x.Title is not null);
        RuleFor(x => x.Title!.Ar).MaximumLength(512).When(x => x.Title is not null);

        RuleFor(x => x.Rationale).NotNull().WithMessage("A rationale is required.");
        RuleFor(x => x.Rationale!).Must(DecisionText.HasEitherLanguage).When(x => x.Rationale is not null).WithMessage("A rationale is required in at least one language.");

        // ConditionallyApproved must carry ≥1 condition (matches the domain guard; caught earlier as 400).
        RuleFor(x => x.Conditions)
            .Must(c => c is { Count: > 0 })
            .When(x => x.Outcome == DecisionOutcome.ConditionallyApproved)
            .WithMessage("A conditionally-approved decision requires at least one condition.");

        // Each condition's text is validated here too — otherwise a null/empty Text would reach the domain
        // ctor and throw (→ 409) instead of a clean field-level 400. At-least-one-language, like the rest.
        RuleForEach(x => x.Conditions).ChildRules(c =>
        {
            c.RuleFor(r => r.Text).NotNull().WithMessage("A condition requires text.");
            c.RuleFor(r => r.Text!).Must(DecisionText.HasEitherLanguage).When(r => r.Text is not null).WithMessage("Condition text is required in at least one language.");
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

        var key = await _keys.NextDecisionKeyAsync(now.Year, ct);
        var conditions = (request.Conditions ?? Array.Empty<DecisionConditionRequest>())
            .Select(c => new DecisionConditionInput(c.Text, c.DueDate));

        var decision = Decision.Draft(key, request.TopicId, request.MeetingId, request.Outcome,
            request.Title, request.Rationale, request.Alternatives, request.VoteId, conditions, sub, now);

        _db.Decisions.Add(decision);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitAsync("Decisions.DecisionDrafted", sub, new { decision.PublicId, decision.Key }, ct);

        return DecisionMapping.ToSummary(decision);
    }
}
