using Acmp.Modules.Governance.Application.Abstractions;
using Acmp.Modules.Governance.Application.Internal;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Contracts.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Governance.Application.Features.ProposeAdr;

// W17: submit an ADR for approval (Draft → Proposed). RBAC = Adr.Create. Fans a review request out to the
// current Reviewer roster (W17 notifications), skipping the proposer. The domain enforces the legal state
// (a wrong-state call → 409).
public sealed record ProposeAdrCommand(Guid AdrId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = AdrRoles.Author;
}

public sealed class ProposeAdrHandler : IRequestHandler<ProposeAdrCommand>
{
    private readonly IGovernanceDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly ICurrentUser _user;
    private readonly ICommitteeDirectory _committee;
    private readonly INotificationChannel _notifications;

    public ProposeAdrHandler(IGovernanceDbContext db, IClock clock, IAuditSink audit, ICurrentUser user,
        ICommitteeDirectory committee, INotificationChannel notifications)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _user = user;
        _committee = committee;
        _notifications = notifications;
    }

    public async Task Handle(ProposeAdrCommand request, CancellationToken ct)
    {
        var adr = await _db.Adrs.FirstOrDefaultAsync(a => a.PublicId == request.AdrId, ct)
            ?? throw new KeyNotFoundException("ADR not found.");

        var (sub, _) = CurrentActor.Of(_user);
        adr.Propose(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitAsync("Governance.AdrProposed", sub, new { adr.PublicId, adr.Key }, ct);

        // W17: notify the reviewers (skip the proposer if they hold the Reviewer role themselves).
        var reviewers = await _committee.GetActiveMembersInRoleAsync(AcmpRoles.Reviewer, ct);
        foreach (var reviewer in reviewers)
            if (!string.Equals(reviewer.UserId, sub, StringComparison.Ordinal))
                await _notifications.PublishAsync(AdrNotifications.ProposedForReview(reviewer.UserId, adr.Key), ct);
    }
}
