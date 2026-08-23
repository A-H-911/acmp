using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Membership.Application.Features.ExpireGuestAccess;

// FR-159 / AC-092 — the recurring sweep that closes a guest's access when their window ends.
//
// HEADLESS: there is no ICurrentUser in a Hangfire scope, so the audit actor is the system — the
// same shape as SweepActionRemindersHandler. All logic lives here and is unit-tested against a fake
// clock; Hangfire only cron-triggers it.
//
// ⚠ THIS IS DEFENCE IN DEPTH, NOT THE ENFORCEMENT. The enforcement is ADR-0039's per-request
// revalidation, which refuses an expired member on their very next request whether or not this
// sweep has run — so a sweep that is late, or disabled, or never scheduled cannot leave a guest with
// live access. That ordering matters: AC-092 asks for BOTH ("the API refuses their requests ... AND
// the Keycloak account is disabled at expiry so the login itself stops working"), and building only
// the sweep would have been the weaker half masquerading as the control.
public sealed record ExpireGuestAccessCommand : IRequest<GuestAccessSweepResult>;

/// <param name="Expired">Members whose window had closed and were still enabled.</param>
/// <param name="IdentityDisabled">
/// How many of those were ALSO disabled in Keycloak. Deliberately reported separately from
/// <paramref name="Expired"/>: when no identity provider is configured the two differ, and a single
/// count would hide that the login itself was never closed.
/// </param>
public sealed record GuestAccessSweepResult(int Expired, int IdentityDisabled);

public sealed class ExpireGuestAccessHandler : IRequestHandler<ExpireGuestAccessCommand, GuestAccessSweepResult>
{
    private readonly IMembershipDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditSink _audit;
    // IEnumerable, not the interface itself: IIdentityProvider is registered ONLY when
    // KeycloakAdmin is configured (ADR-0038), so demanding it would make this sweep uninjectable —
    // and therefore the whole worker unbootable — in every environment that has not switched in-app
    // user management on. Empty here simply means the Keycloak half is skipped.
    private readonly IEnumerable<IIdentityProvider> _identity;

    public ExpireGuestAccessHandler(
        IMembershipDbContext db, IClock clock, IAuditSink audit, IEnumerable<IIdentityProvider> identity)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _identity = identity;
    }

    public async Task<GuestAccessSweepResult> Handle(ExpireGuestAccessCommand request, CancellationToken ct)
    {
        var now = _clock.UtcNow;

        // Status != Disabled is what makes this IDEMPOTENT: a member disabled by an earlier run is
        // not picked up again, so re-running the sweep (or running it more often than scheduled)
        // neither re-audits nor re-calls Keycloak.
        var expired = await _db.Members
            .Where(m => m.AccessExpiresAt != null && m.AccessExpiresAt < now && m.Status != MembershipStatus.Disabled)
            .ToListAsync(ct);

        if (expired.Count == 0)
            return new GuestAccessSweepResult(0, 0);

        var identity = _identity.FirstOrDefault();
        var identityDisabled = 0;

        foreach (var member in expired)
        {
            // LOCAL FIRST, and this order is the point. The local status is what ADR-0039's
            // revalidation reads, so it is the half that actually stops access; the Keycloak call is
            // a remote operation that can fail. Disabling locally first means a Keycloak outage
            // leaves access CLOSED with the login still open, rather than the reverse.
            member.Deactivate();

            if (identity is not null)
            {
                // Disable, NEVER delete: deleting a Keycloak user strands its member row forever
                // (DEF-029), and this row is the guest's audit attribution.
                await identity.DisableUserAsync(member.KeycloakUserId, ct);
                identityDisabled++;
            }

            await _audit.EmitEnrichedAsync(
                "Membership.GuestAccessExpired", nameof(Domain.CommitteeMember), member.PublicId.ToString(), ct: ct);
        }

        await _db.SaveChangesAsync(ct);
        return new GuestAccessSweepResult(expired.Count, identityDisabled);
    }
}
