using Acmp.Modules.Governance.Application.Abstractions;
using Acmp.Modules.Governance.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Governance.Application.Features.ApproveAdr;

// W17: approve an ADR (Proposed → Approved, immutable thereafter). RBAC = Adr.Approve (Chairman/Secretary).
// SoD posture = SOFT (operator decision 2026-07-04): the author MAY approve their own ADR, but the fact is
// recorded in the audit payload (AuthorApprovedSelf) — right-sized for a ≤20-user committee where the
// Secretary is often the sole author. Stakeholders (the committee) are notified on approval (W17).
public sealed record ApproveAdrCommand(Guid AdrId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = AdrRoles.Approver;
}

public sealed class ApproveAdrHandler : IRequestHandler<ApproveAdrCommand>
{
    private readonly IGovernanceDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _user;
    private readonly ICommitteeDirectory _committee;
    private readonly INotificationChannel _notifications;

    public ApproveAdrHandler(IGovernanceDbContext db, IClock clock, IAuditSink audit, ICurrentUser user,
        ICommitteeDirectory committee, INotificationChannel notifications)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _user = user;
        _committee = committee;
        _notifications = notifications;
    }

    public async Task Handle(ApproveAdrCommand request, CancellationToken ct)
    {
        var adr = await _db.Adrs.FirstOrDefaultAsync(a => a.PublicId == request.AdrId, ct)
            ?? throw new KeyNotFoundException("ADR not found.");

        var (sub, name) = CurrentActor.Of(_user);
        var authorApprovedSelf = string.Equals(adr.AuthorUserId, sub, StringComparison.Ordinal);

        adr.Approve(sub, name, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        // High-importance governance event; SoD-soft posture recorded (guardrail 4, operator 2026-07-04).
        await _audit.EmitAsync("Governance.AdrApproved", sub,
            new { adr.PublicId, adr.Key, AuthorApprovedSelf = authorApprovedSelf }, ct);

        // W17: notify the committee that the ADR is approved and in force (skip the approver).
        await AdrNotifications.FanOutAsync(_committee, _notifications, AdrNotifications.Approved(adr.Key), sub, ct);
    }
}
