using Acmp.Modules.Governance.Application.Abstractions;
using Acmp.Modules.Governance.Application.Contracts;
using Acmp.Modules.Governance.Application.Features.CreateAdr;
using Acmp.Modules.Governance.Application.Internal;
using Acmp.Modules.Governance.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Governance.Application.Features.SupersedeAdr;

// W21 (ADR half): replace an approved ADR with a corrected one. In ONE transaction: author a NEW ADR,
// approve it, link the two directions, then supersede the prior — so the successor reaches Approved BEFORE
// the prior flips to Superseded (W21 ordering, FR-101). The prior must be Approved (404 if missing, 409 if
// not approvable). RBAC = Adr.Supersede (Chairman/Secretary). Both records are preserved and immutable
// (ADR-0009, supersede-not-edit). The committee is notified of the replacement.
public sealed record SupersedeAdrCommand(
    Guid PriorAdrId,
    LocalizedString Title,
    LocalizedString Context,
    LocalizedString? DecisionDrivers,
    LocalizedString DecisionText,
    LocalizedString? ConsequencesPositive,
    LocalizedString? ConsequencesNegative,
    IReadOnlyList<AdrOptionRequest>? Options,
    LocalizedString Reason) : IRequest<AdrSummaryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = AdrRoles.Approver;
}

public sealed class SupersedeAdrValidator : AbstractValidator<SupersedeAdrCommand>
{
    public SupersedeAdrValidator()
    {
        RuleFor(x => x.PriorAdrId).NotEmpty();
        RuleFor(x => x.Title).NotNull().WithMessage("A title is required.");
        RuleFor(x => x.Title!.En).NotEmpty().MaximumLength(512).When(x => x.Title is not null).WithMessage("Title (EN) is required (max 512).");
        RuleFor(x => x.Title!.Ar).NotEmpty().MaximumLength(512).When(x => x.Title is not null).WithMessage("Title (AR) is required (max 512).");
        RuleFor(x => x.Context).NotNull().WithMessage("A context is required.");
        RuleFor(x => x.Context!.En).NotEmpty().When(x => x.Context is not null).WithMessage("Context (EN) is required.");
        RuleFor(x => x.Context!.Ar).NotEmpty().When(x => x.Context is not null).WithMessage("Context (AR) is required.");
        RuleFor(x => x.DecisionText).NotNull().WithMessage("A decision is required.");
        RuleFor(x => x.DecisionText!.En).NotEmpty().When(x => x.DecisionText is not null).WithMessage("Decision (EN) is required.");
        RuleFor(x => x.DecisionText!.Ar).NotEmpty().When(x => x.DecisionText is not null).WithMessage("Decision (AR) is required.");
        RuleFor(x => x.Reason).NotNull().WithMessage("A supersession reason is required.");
        RuleFor(x => x.Reason!.En).NotEmpty().When(x => x.Reason is not null).WithMessage("Supersession reason (EN) is required.");
        RuleFor(x => x.Reason!.Ar).NotEmpty().When(x => x.Reason is not null).WithMessage("Supersession reason (AR) is required.");
        RuleForEach(x => x.Options).ChildRules(o =>
        {
            o.RuleFor(r => r.Name).NotNull().WithMessage("An option requires a name.");
            o.RuleFor(r => r.Name!.En).NotEmpty().When(r => r.Name is not null).WithMessage("Option name (EN) is required.");
            o.RuleFor(r => r.Name!.Ar).NotEmpty().When(r => r.Name is not null).WithMessage("Option name (AR) is required.");
        });
    }
}

public sealed class SupersedeAdrHandler : IRequestHandler<SupersedeAdrCommand, AdrSummaryDto>
{
    private readonly IGovernanceDbContext _db;
    private readonly IAdrKeyGenerator _keys;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICommitteeDirectory _committee;
    private readonly INotificationChannel _notifications;

    public SupersedeAdrHandler(IGovernanceDbContext db, IAdrKeyGenerator keys, ICurrentUser user, IClock clock,
        IAuditSink audit, ICommitteeDirectory committee, INotificationChannel notifications)
    {
        _db = db;
        _keys = keys;
        _user = user;
        _clock = clock;
        _audit = audit;
        _committee = committee;
        _notifications = notifications;
    }

    public async Task<AdrSummaryDto> Handle(SupersedeAdrCommand request, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var (sub, name) = CurrentActor.Of(_user);

        var prior = await _db.Adrs.FirstOrDefaultAsync(a => a.PublicId == request.PriorAdrId, ct)
            ?? throw new KeyNotFoundException("ADR not found.");

        var key = await _keys.NextAdrKeyAsync(now.Year, ct);
        var options = (request.Options ?? Array.Empty<AdrOptionRequest>())
            .Select(o => new AdrOptionInput(o.Name, o.Body, o.IsChosen));

        // Successor is authored + approved before the prior is superseded (W21 ordering).
        var successor = Adr.Draft(key, request.Title, request.Context, request.DecisionDrivers, request.DecisionText,
            request.ConsequencesPositive, request.ConsequencesNegative, options, sub, name, sourceDecisionId: null, now);
        successor.Propose(now);
        successor.Approve(sub, name, now);
        successor.MarkSupersedes(prior.PublicId);
        _db.Adrs.Add(successor);

        prior.Supersede(successor.PublicId, request.Reason, now);
        await _db.SaveChangesAsync(ct);

        // The successor is born Approved via this supersession — record its own approval so the new record has an
        // audit row of its own (not only the prior's supersede event); ViaSupersession marks how it was approved.
        await _audit.EmitAsync("Governance.AdrApproved", sub,
            new { successor.PublicId, successor.Key, ViaSupersession = true, PriorKey = prior.Key }, ct);

        await _audit.EmitAsync("Governance.AdrSuperseded", sub,
            new { prior.PublicId, prior.Key, SupersededBy = successor.PublicId, SuccessorKey = successor.Key }, ct);

        // W21: notify the committee of the replacement (skip the actor).
        await AdrNotifications.FanOutAsync(_committee, _notifications,
            AdrNotifications.Superseded(prior.Key, successor.Key), sub, ct);

        return AdrMapping.ToSummary(successor);
    }
}
