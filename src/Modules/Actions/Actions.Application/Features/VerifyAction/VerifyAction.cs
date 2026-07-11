using Acmp.Modules.Actions.Application.Abstractions;
using Acmp.Modules.Actions.Application.Internal;
using Acmp.Modules.Actions.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Exceptions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Actions.Application.Features.VerifyAction;

// W14 (verify): an independent verifier confirms a Completed action (Completed → Verified). RBAC =
// Action.Verify (Chairman/Secretary; Member allow-if-owner). The SoD-1 hard guard (AC-012/013): the
// verifier may be neither the action's owner nor the person who marked it complete — a violation is a
// Forbidden (403), regardless of role, and the DENIED attempt is audited before the refusal. On success the
// owner is notified their action was verified + closed.
public sealed record VerifyActionCommand(Guid ActionId) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } =
        new[] { AcmpRoles.Chairman, AcmpRoles.Secretary, AcmpRoles.Member };
}

public sealed class VerifyActionHandler : IRequestHandler<VerifyActionCommand>
{
    private readonly IActionsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    private readonly INotificationChannel _notifications;

    public VerifyActionHandler(IActionsDbContext db, ICurrentUser user, IClock clock,
        IAuditSink audit, INotificationChannel notifications)
    {
        _db = db;
        _user = user;
        _clock = clock;
        _audit = audit;
        _notifications = notifications;
    }

    public async Task Handle(VerifyActionCommand request, CancellationToken ct)
    {
        var action = await _db.Actions.FirstOrDefaultAsync(a => a.PublicId == request.ActionId, ct)
            ?? throw new KeyNotFoundException("Action not found.");

        var (sub, name) = CurrentActor.Of(_user);

        // SoD-1 (AC-012): audit the denied attempt, then refuse with 403.
        if (!SegregationOfDuties.CanVerifyAction(sub, action.OwnerUserId, action.CompletedByUserId))
        {
            await _audit.EmitEnrichedAsync("Actions.ActionVerifyDenied", nameof(ActionItem), action.PublicId.ToString(), AuditOutcome.Denied, ct);
            throw new ForbiddenAccessException("An action's verifier cannot be its owner or the person who completed it.");
        }

        action.Verify(sub, name, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        await _audit.EmitEnrichedAsync("Actions.ActionVerified", nameof(ActionItem), action.PublicId.ToString(), ct: ct);
        if (!string.Equals(action.OwnerUserId, sub, StringComparison.Ordinal))
            await _notifications.PublishAsync(ActionNotifications.Verified(action.OwnerUserId, action.Key), ct);
    }
}
