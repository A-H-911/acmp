using Acmp.Modules.Governance.Application.Abstractions;
using Acmp.Modules.Governance.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Governance.Application.Features.ProposeInvariant;

// W18: submit an invariant for approval (Draft → Proposed). RBAC = Invariant.Create. Fans a review request
// out to the current Reviewer roster (W18 notifications), skipping the proposer. The domain enforces the
// legal state (a wrong-state call → 409).
public sealed record ProposeInvariantCommand(Guid InvariantId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = InvariantRoles.Author;
}

public sealed class ProposeInvariantHandler : IRequestHandler<ProposeInvariantCommand>
{
    private readonly IGovernanceDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _user;
    private readonly ICommitteeDirectory _committee;
    private readonly INotificationChannel _notifications;

    public ProposeInvariantHandler(IGovernanceDbContext db, IClock clock, IAuditSink audit, ICurrentUser user,
        ICommitteeDirectory committee, INotificationChannel notifications)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _user = user;
        _committee = committee;
        _notifications = notifications;
    }

    public async Task Handle(ProposeInvariantCommand request, CancellationToken ct)
    {
        var inv = await _db.Invariants.FirstOrDefaultAsync(a => a.PublicId == request.InvariantId, ct)
            ?? throw new KeyNotFoundException("Invariant not found.");

        var (sub, _) = CurrentActor.Of(_user);
        inv.Propose(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitAsync("Governance.InvariantProposed", sub, new { inv.PublicId, inv.Key }, ct);

        // W18: notify the reviewers (skip the proposer if they hold the Reviewer role themselves).
        var reviewers = await _committee.GetActiveMembersInRoleAsync(AcmpRoles.Reviewer, ct);
        foreach (var reviewer in reviewers)
            if (!string.Equals(reviewer.UserId, sub, StringComparison.Ordinal))
                await _notifications.PublishAsync(InvariantNotifications.ProposedForReview(reviewer.UserId, inv.Key), ct);
    }
}
