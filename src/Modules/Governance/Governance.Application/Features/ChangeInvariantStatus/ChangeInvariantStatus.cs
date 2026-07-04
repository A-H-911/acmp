using Acmp.Modules.Governance.Application.Abstractions;
using Acmp.Modules.Governance.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Governance.Application.Features.ChangeInvariantStatus;

// The two remaining invariant transitions that are neither create/propose/approve nor supersede:
//  · request changes (Proposed → Draft) — a plain audited transition (RBAC = Invariant.Create, reviewers incl).
//  · retire (Active → Retired) — take an invariant out of force without a replacement (RBAC = Invariant.
//    Approve), then notify the committee (W21). The domain enforces the legal state machine (wrong state → 409).

// ── Request changes: Proposed → Draft ────────────────────────────────────────────────────────────────────
public sealed record RequestInvariantChangesCommand(Guid InvariantId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = InvariantRoles.Author;
}

public sealed class RequestInvariantChangesHandler : IRequestHandler<RequestInvariantChangesCommand>
{
    private readonly IGovernanceDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _user;

    public RequestInvariantChangesHandler(IGovernanceDbContext db, IClock clock, IAuditSink audit, ICurrentUser user)
        => (_db, _clock, _audit, _user) = (db, clock, audit, user);

    public async Task Handle(RequestInvariantChangesCommand request, CancellationToken ct)
    {
        var inv = await _db.Invariants.FirstOrDefaultAsync(a => a.PublicId == request.InvariantId, ct)
            ?? throw new KeyNotFoundException("Invariant not found.");
        var (sub, _) = CurrentActor.Of(_user);
        inv.RequestChanges(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        await _audit.EmitAsync("Governance.InvariantChangesRequested", sub, new { inv.PublicId, inv.Key }, ct);
    }
}

// ── Retire: Active → Retired (rationale required) ─────────────────────────────────────────────────────────
public sealed record RetireInvariantCommand(Guid InvariantId, LocalizedString Reason) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = InvariantRoles.Approver;
}

public sealed class RetireInvariantValidator : AbstractValidator<RetireInvariantCommand>
{
    public RetireInvariantValidator()
    {
        RuleFor(x => x.Reason).NotNull().WithMessage("A retirement rationale is required.");
        RuleFor(x => x.Reason!.En).NotEmpty().When(x => x.Reason is not null).WithMessage("Retirement rationale (EN) is required.");
        RuleFor(x => x.Reason!.Ar).NotEmpty().When(x => x.Reason is not null).WithMessage("Retirement rationale (AR) is required.");
    }
}

public sealed class RetireInvariantHandler : IRequestHandler<RetireInvariantCommand>
{
    private readonly IGovernanceDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _user;
    private readonly ICommitteeDirectory _committee;
    private readonly INotificationChannel _notifications;

    public RetireInvariantHandler(IGovernanceDbContext db, IClock clock, IAuditSink audit, ICurrentUser user,
        ICommitteeDirectory committee, INotificationChannel notifications)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _user = user;
        _committee = committee;
        _notifications = notifications;
    }

    public async Task Handle(RetireInvariantCommand request, CancellationToken ct)
    {
        var inv = await _db.Invariants.FirstOrDefaultAsync(a => a.PublicId == request.InvariantId, ct)
            ?? throw new KeyNotFoundException("Invariant not found.");
        var (sub, _) = CurrentActor.Of(_user);
        inv.Retire(request.Reason, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        await _audit.EmitAsync("Governance.InvariantRetired", sub, new { inv.PublicId, inv.Key }, ct);
        // W21: notify the committee of the retirement (no successor key).
        await InvariantNotifications.FanOutAsync(_committee, _notifications,
            InvariantNotifications.Superseded(inv.Key, successorKey: null), sub, ct);
    }
}
