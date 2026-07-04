using Acmp.Modules.Governance.Application.Abstractions;
using Acmp.Modules.Governance.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Governance.Application.Features.ChangeAdrStatus;

// The two remaining ADR transitions that are neither create/propose/approve nor supersede:
//  · request changes (Proposed → Draft) — a plain audited transition (RBAC = Adr.Create, reviewers included).
//  · deprecate (Approved → Deprecated) — retire an ADR without a replacement (RBAC = Adr.Supersede), then
//    notify the committee (W21). The domain enforces the legal state machine (a wrong-state call → 409).

// ── Request changes: Proposed → Draft ────────────────────────────────────────────────────────────────────
public sealed record RequestAdrChangesCommand(Guid AdrId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = AdrRoles.Author;
}

public sealed class RequestAdrChangesHandler : IRequestHandler<RequestAdrChangesCommand>
{
    private readonly IGovernanceDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _user;

    public RequestAdrChangesHandler(IGovernanceDbContext db, IClock clock, IAuditSink audit, ICurrentUser user)
        => (_db, _clock, _audit, _user) = (db, clock, audit, user);

    public async Task Handle(RequestAdrChangesCommand request, CancellationToken ct)
    {
        var adr = await _db.Adrs.FirstOrDefaultAsync(a => a.PublicId == request.AdrId, ct)
            ?? throw new KeyNotFoundException("ADR not found.");
        var (sub, _) = CurrentActor.Of(_user);
        adr.RequestChanges(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        await _audit.EmitAsync("Governance.AdrChangesRequested", sub, new { adr.PublicId, adr.Key }, ct);
    }
}

// ── Deprecate: Approved → Deprecated (rationale required) ─────────────────────────────────────────────────
public sealed record DeprecateAdrCommand(Guid AdrId, LocalizedString Reason) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = AdrRoles.Approver;
}

public sealed class DeprecateAdrValidator : AbstractValidator<DeprecateAdrCommand>
{
    public DeprecateAdrValidator()
    {
        RuleFor(x => x.Reason).NotNull().WithMessage("A deprecation rationale is required.");
        RuleFor(x => x.Reason!.En).NotEmpty().When(x => x.Reason is not null).WithMessage("Deprecation rationale (EN) is required.");
        RuleFor(x => x.Reason!.Ar).NotEmpty().When(x => x.Reason is not null).WithMessage("Deprecation rationale (AR) is required.");
    }
}

public sealed class DeprecateAdrHandler : IRequestHandler<DeprecateAdrCommand>
{
    private readonly IGovernanceDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _user;
    private readonly ICommitteeDirectory _committee;
    private readonly INotificationChannel _notifications;

    public DeprecateAdrHandler(IGovernanceDbContext db, IClock clock, IAuditSink audit, ICurrentUser user,
        ICommitteeDirectory committee, INotificationChannel notifications)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _user = user;
        _committee = committee;
        _notifications = notifications;
    }

    public async Task Handle(DeprecateAdrCommand request, CancellationToken ct)
    {
        var adr = await _db.Adrs.FirstOrDefaultAsync(a => a.PublicId == request.AdrId, ct)
            ?? throw new KeyNotFoundException("ADR not found.");
        var (sub, _) = CurrentActor.Of(_user);
        adr.Deprecate(request.Reason, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        await _audit.EmitAsync("Governance.AdrDeprecated", sub, new { adr.PublicId, adr.Key }, ct);
        // W21: notify the committee of the deprecation (no successor key).
        await AdrNotifications.FanOutAsync(_committee, _notifications,
            AdrNotifications.Superseded(adr.Key, successorKey: null), sub, ct);
    }
}
