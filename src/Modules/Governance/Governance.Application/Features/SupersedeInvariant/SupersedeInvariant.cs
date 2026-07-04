using Acmp.Modules.Governance.Application.Abstractions;
using Acmp.Modules.Governance.Application.Contracts;
using Acmp.Modules.Governance.Application.Internal;
using Acmp.Modules.Governance.Domain;
using Acmp.Modules.Governance.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Governance.Application.Features.SupersedeInvariant;

// W21 (invariant half): replace an active invariant with a corrected one. In ONE transaction: author a NEW
// invariant, activate it, link the two directions, then supersede the prior — so the successor reaches Active
// BEFORE the prior flips to Superseded (W21 ordering). The prior must be Active (404 if missing, 409 if not
// supersedable). RBAC = Invariant.Approve (Chairman/Secretary). Both records are preserved and immutable
// (ADR-0009, supersede-not-edit). The committee is notified of the replacement.
public sealed record SupersedeInvariantCommand(
    Guid PriorInvariantId,
    InvariantCategory Category,
    InvariantScope Scope,
    LocalizedString Statement,
    LocalizedString Rationale,
    LocalizedString? ExceptionsPolicy,
    string OwnerUserId,
    string OwnerName,
    LocalizedString Reason) : IRequest<InvariantSummaryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = InvariantRoles.Approver;
}

public sealed class SupersedeInvariantValidator : AbstractValidator<SupersedeInvariantCommand>
{
    public SupersedeInvariantValidator()
    {
        RuleFor(x => x.PriorInvariantId).NotEmpty();
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Scope).IsInEnum();
        RuleFor(x => x.OwnerUserId).NotEmpty().WithMessage("An owner is required.");
        RuleFor(x => x.OwnerName).NotEmpty().MaximumLength(256).WithMessage("An owner name is required (max 256).");
        RuleFor(x => x.Statement).NotNull().WithMessage("A statement is required.");
        RuleFor(x => x.Statement!.En).NotEmpty().When(x => x.Statement is not null).WithMessage("Statement (EN) is required.");
        RuleFor(x => x.Statement!.Ar).NotEmpty().When(x => x.Statement is not null).WithMessage("Statement (AR) is required.");
        RuleFor(x => x.Rationale).NotNull().WithMessage("A rationale is required.");
        RuleFor(x => x.Rationale!.En).NotEmpty().When(x => x.Rationale is not null).WithMessage("Rationale (EN) is required.");
        RuleFor(x => x.Rationale!.Ar).NotEmpty().When(x => x.Rationale is not null).WithMessage("Rationale (AR) is required.");
        RuleFor(x => x.Reason).NotNull().WithMessage("A supersession reason is required.");
        RuleFor(x => x.Reason!.En).NotEmpty().When(x => x.Reason is not null).WithMessage("Supersession reason (EN) is required.");
        RuleFor(x => x.Reason!.Ar).NotEmpty().When(x => x.Reason is not null).WithMessage("Supersession reason (AR) is required.");
    }
}

public sealed class SupersedeInvariantHandler : IRequestHandler<SupersedeInvariantCommand, InvariantSummaryDto>
{
    private readonly IGovernanceDbContext _db;
    private readonly IInvariantKeyGenerator _keys;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICommitteeDirectory _committee;
    private readonly INotificationChannel _notifications;

    public SupersedeInvariantHandler(IGovernanceDbContext db, IInvariantKeyGenerator keys, ICurrentUser user,
        IClock clock, IAuditSink audit, ICommitteeDirectory committee, INotificationChannel notifications)
    {
        _db = db;
        _keys = keys;
        _user = user;
        _clock = clock;
        _audit = audit;
        _committee = committee;
        _notifications = notifications;
    }

    public async Task<InvariantSummaryDto> Handle(SupersedeInvariantCommand request, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var (sub, name) = CurrentActor.Of(_user);

        var prior = await _db.Invariants.FirstOrDefaultAsync(a => a.PublicId == request.PriorInvariantId, ct)
            ?? throw new KeyNotFoundException("Invariant not found.");

        var key = await _keys.NextInvariantKeyAsync(now.Year, ct);

        // Successor is authored + activated before the prior is superseded (W21 ordering).
        var successor = Invariant.Draft(key, request.Category, request.Scope, request.Statement, request.Rationale,
            request.ExceptionsPolicy, request.OwnerUserId, request.OwnerName, now);
        successor.Propose(now);
        successor.Activate(sub, name, now);
        successor.MarkSupersedes(prior.PublicId);
        _db.Invariants.Add(successor);

        prior.Supersede(successor.PublicId, request.Reason, now);
        await _db.SaveChangesAsync(ct);

        // The successor is born Active via this supersession — record its own activation so the new record has an
        // audit row of its own (not only the prior's supersede event); ViaSupersession marks how it was activated.
        await _audit.EmitAsync("Governance.InvariantActivated", sub,
            new { successor.PublicId, successor.Key, ViaSupersession = true, PriorKey = prior.Key }, ct);

        await _audit.EmitAsync("Governance.InvariantSuperseded", sub,
            new { prior.PublicId, prior.Key, SupersededBy = successor.PublicId, SuccessorKey = successor.Key }, ct);

        // W21: notify the committee of the replacement (skip the actor).
        await InvariantNotifications.FanOutAsync(_committee, _notifications,
            InvariantNotifications.Superseded(prior.Key, successor.Key), sub, ct);

        return InvariantMapping.ToSummary(successor);
    }
}
