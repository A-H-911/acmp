using Acmp.Modules.Governance.Application.Abstractions;
using Acmp.Modules.Governance.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Governance.Application.Features.ApproveInvariant;

// W18: approve/activate an invariant (Proposed → Active, immutable thereafter). RBAC = Invariant.Approve
// (Chairman/Secretary). SoD posture = SOFT (operator decision 2026-07-04, mirrors Decisions SoD-2): the
// AUTHOR MAY approve their own invariant, but the fact is recorded in the audit payload (AuthorApprovedSelf)
// — right-sized for a ≤20-user committee where the Secretary is often the sole author. "Author" = the
// server-derived creator (CreatedBy), NOT the client-supplied Owner field (which may name a third party);
// comparing CreatedBy is the same self-approval signal ADR records and is not gameable. Committee is notified.
public sealed record ApproveInvariantCommand(Guid InvariantId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = InvariantRoles.Approver;
}

public sealed class ApproveInvariantHandler : IRequestHandler<ApproveInvariantCommand>
{
    private readonly IGovernanceDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _user;
    private readonly ICommitteeDirectory _committee;
    private readonly INotificationChannel _notifications;

    public ApproveInvariantHandler(IGovernanceDbContext db, IClock clock, IAuditSink audit, ICurrentUser user,
        ICommitteeDirectory committee, INotificationChannel notifications)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _user = user;
        _committee = committee;
        _notifications = notifications;
    }

    public async Task Handle(ApproveInvariantCommand request, CancellationToken ct)
    {
        var inv = await _db.Invariants.FirstOrDefaultAsync(a => a.PublicId == request.InvariantId, ct)
            ?? throw new KeyNotFoundException("Invariant not found.");

        var (sub, name) = CurrentActor.Of(_user);
        var authorApprovedSelf = string.Equals(inv.CreatedBy, sub, StringComparison.Ordinal);

        inv.Activate(sub, name, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        // High-importance governance event; SoD-soft posture recorded (guardrail 4, operator 2026-07-04).
        await _audit.EmitAsync("Governance.InvariantActivated", sub,
            new { inv.PublicId, inv.Key, AuthorApprovedSelf = authorApprovedSelf }, ct);

        // W18: notify the committee that the invariant is active and in force (skip the approver).
        await InvariantNotifications.FanOutAsync(_committee, _notifications, InvariantNotifications.Activated(inv.Key), sub, ct);
    }
}
