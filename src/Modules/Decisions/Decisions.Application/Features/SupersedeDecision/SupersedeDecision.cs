using Acmp.Modules.Decisions.Application.Abstractions;
using Acmp.Modules.Decisions.Application.Contracts;
using Acmp.Modules.Decisions.Application.Internal;
using Acmp.Modules.Decisions.Domain;
using Acmp.Modules.Decisions.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Contracts.Topics;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Decisions.Application.Features.SupersedeDecision;

// W21 (decision half): replace an issued decision with a corrected one. In ONE transaction: draft a NEW
// decision, issue it, then supersede the prior — so the successor reaches Issued BEFORE the prior flips to
// Superseded (W21 ordering). The prior must be Issued (404 if missing, 409 if not issuable). RBAC =
// Decision.ChairApprove (Chairman). The new decision inherits the prior's topic + meeting. Both the new
// DecisionIssued and the DecisionSuperseded are audited (AC-028). ponytail: the successor is a genuine
// issued decision, so it produces the SAME issue side-effects as IssueDecision (Topic→Decided seam, which
// is idempotent, + the committee notification) — deliberately, not by omission.
public sealed record SupersedeDecisionCommand(
    Guid PriorDecisionId,
    DecisionOutcome Outcome,
    LocalizedString Title,
    LocalizedString Statement,
    LocalizedString Rationale,
    LocalizedString? Alternatives,
    IReadOnlyList<DecisionConditionRequest> Conditions,
    LocalizedString Reason) : IRequest<DecisionSummaryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Chairman };
}

public sealed class SupersedeDecisionValidator : AbstractValidator<SupersedeDecisionCommand>
{
    public SupersedeDecisionValidator()
    {
        RuleFor(x => x.PriorDecisionId).NotEmpty();
        RuleFor(x => x.Outcome).IsInEnum();

        // Content is mirrored to both columns (FTS), so both EN and AR must be present. Title keeps its
        // per-language nvarchar(512) max so an over-long title is a clean 400, not a SaveChanges 500.
        RuleFor(x => x.Title).NotNull().WithMessage("A title is required.");
        RuleFor(x => x.Title!.En).NotEmpty().MaximumLength(512).When(x => x.Title is not null).WithMessage("Title (EN) is required (max 512).");
        RuleFor(x => x.Title!.Ar).NotEmpty().MaximumLength(512).When(x => x.Title is not null).WithMessage("Title (AR) is required (max 512).");

        RuleFor(x => x.Statement).NotNull().WithMessage("A decision statement is required.");
        RuleFor(x => x.Statement!.En).NotEmpty().MaximumLength(2000).When(x => x.Statement is not null).WithMessage("Statement (EN) is required (max 2000).");
        RuleFor(x => x.Statement!.Ar).NotEmpty().MaximumLength(2000).When(x => x.Statement is not null).WithMessage("Statement (AR) is required (max 2000).");

        RuleFor(x => x.Rationale).NotNull().WithMessage("A rationale is required.");
        RuleFor(x => x.Rationale!.En).NotEmpty().When(x => x.Rationale is not null).WithMessage("Rationale (EN) is required.");
        RuleFor(x => x.Rationale!.Ar).NotEmpty().When(x => x.Rationale is not null).WithMessage("Rationale (AR) is required.");

        RuleFor(x => x.Reason).NotNull().WithMessage("A supersession reason is required.");
        RuleFor(x => x.Reason!.En).NotEmpty().When(x => x.Reason is not null).WithMessage("Supersession reason (EN) is required.");
        RuleFor(x => x.Reason!.Ar).NotEmpty().When(x => x.Reason is not null).WithMessage("Supersession reason (AR) is required.");

        RuleFor(x => x.Conditions)
            .Must(c => c is { Count: > 0 })
            .When(x => x.Outcome == DecisionOutcome.ConditionallyApproved)
            .WithMessage("A conditionally-approved decision requires at least one condition.");

        // Each condition's bilingual text is validated here too (clean 400 instead of the domain 409).
        RuleForEach(x => x.Conditions).ChildRules(c =>
        {
            c.RuleFor(r => r.Text).NotNull().WithMessage("A condition requires text.");
            c.RuleFor(r => r.Text!.En).NotEmpty().When(r => r.Text is not null).WithMessage("Condition text (EN) is required.");
            c.RuleFor(r => r.Text!.Ar).NotEmpty().When(r => r.Text is not null).WithMessage("Condition text (AR) is required.");
        });
    }
}

public sealed class SupersedeDecisionHandler : IRequestHandler<SupersedeDecisionCommand, DecisionSummaryDto>
{
    private readonly IDecisionsDbContext _db;
    private readonly IDecisionKeyGenerator _keys;
    private readonly ITopicDecisionRecorder _topics;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICommitteeDirectory _directory;
    private readonly INotificationChannel _notifications;

    public SupersedeDecisionHandler(IDecisionsDbContext db, IDecisionKeyGenerator keys,
        ITopicDecisionRecorder topics, ICurrentUser user, IClock clock, IAuditSink audit,
        ICommitteeDirectory directory, INotificationChannel notifications)
    {
        _db = db;
        _keys = keys;
        _topics = topics;
        _user = user;
        _clock = clock;
        _audit = audit;
        _directory = directory;
        _notifications = notifications;
    }

    public async Task<DecisionSummaryDto> Handle(SupersedeDecisionCommand request, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var (sub, name) = CurrentActor.Of(_user);

        var prior = await _db.Decisions.FirstOrDefaultAsync(d => d.PublicId == request.PriorDecisionId, ct)
            ?? throw new KeyNotFoundException("Decision not found.");

        var key = await _keys.NextDecisionKeyAsync(now.Year, ct);
        var conditions = (request.Conditions ?? Array.Empty<DecisionConditionRequest>())
            .Select(c => new DecisionConditionInput(c.Text, c.DueDate));

        // Successor inherits the prior's topic + meeting; it reaches Issued before the prior is superseded.
        var successor = Decision.Draft(key, prior.TopicId, prior.MeetingId, request.Outcome,
            request.Title, request.Statement, request.Rationale, request.Alternatives, voteId: null, conditions, sub, now);
        successor.Issue(sub, name, chairOverride: false, overrideJustification: null, now);
        _db.Decisions.Add(successor);

        prior.Supersede(successor.PublicId, request.Reason, now);

        await _db.SaveChangesAsync(ct);

        await _audit.EmitAsync("Decisions.DecisionSuperseded", sub,
            new { prior.PublicId, prior.Key, SupersededBy = successor.PublicId, SuccessorKey = successor.Key }, ct);
        await DecisionIssuance.ApplyAsync(_topics, _directory, _notifications, _audit,
            sub, successor.TopicId, successor.Key, chairOverride: false, ct);

        return DecisionMapping.ToSummary(successor);
    }
}
