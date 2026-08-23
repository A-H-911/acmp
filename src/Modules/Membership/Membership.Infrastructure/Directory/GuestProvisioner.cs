using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Application.Internal;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Contracts.Membership;

namespace Acmp.Modules.Membership.Infrastructure.Directory;

// Membership-owned implementation of the shared IGuestProvisioner write port (ADR-0021, ADR-0040
// decision 1): the Meetings module invites a guest presenter without reaching into this module's
// store or its Keycloak client. Mirrors CommitteeDirectory on the read side.
//
// It delegates to MemberInvitation, the same body the in-app invite (FR-156) runs, so the duplicate
// check, the Keycloak-before-local ordering and the audit emission are shared rather than copied.
// The ONLY difference between the two callers is the access window and the audit verb.
//
// REGISTERED ONLY WHEN THE KEYCLOAK ADMIN CLIENT IS CONFIGURED, exactly like the invite endpoint it
// mirrors: without an identity provider this cannot create the account, and creating the member row
// alone would leave a person who can never sign in — a row DEF-029 says can be disabled but never
// deleted. Absent is better than half-done.
public sealed class GuestProvisioner : IGuestProvisioner
{
    private readonly IMembershipDbContext _db;
    private readonly IIdentityProvider _identity;
    private readonly IAuditSink _audit;
    private readonly IClock _clock;

    public GuestProvisioner(IMembershipDbContext db, IIdentityProvider identity, IAuditSink audit, IClock clock)
    {
        _db = db;
        _identity = identity;
        _audit = audit;
        _clock = clock;
    }

    public async Task<ProvisionedGuest> InviteGuestAsync(
        string email, string fullName, DateTimeOffset accessExpiresAt, CancellationToken ct = default)
    {
        // ⚠ THE GUEST REALM ROLE IS GRANTED HERE AND NOWHERE ELSE (DEF-076 / SC-012). FR-156's invite
        // deliberately creates an inert account for an administrator to give a role to later; a guest
        // presenter has no such follow-up actor, so the Secretary's single action has to produce a
        // principal who can actually sign in and reach /session. Without this the guest got a member
        // row saying Guest with a timed window, over a Keycloak account holding no role at all — so
        // every AllowedRoles check refused them, and GuestSurfaceMiddleware did not even recognise
        // them as a guest, because IsGuestOnly reads the token rather than the row.
        var (member, temporaryPassword) = await MemberInvitation.InviteAsync(
            _db, _identity, _audit, _clock,
            email, fullName, CommitteeRole.Guest, accessExpiresAt,
            auditAction: "Membership.GuestPresenterInvited", ct,
            realmRoles: new[] { AcmpRoles.Guest });

        return new ProvisionedGuest(member.PublicId, member.FullName, member.Email, temporaryPassword);
    }
}
