using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Membership;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Membership.Infrastructure.Identity;

// Membership-owned implementation of the shared IPrincipalRevalidator port (ADR-0039 / ADR-0001):
// the API host asks whether a validated token's subject may still act, without reading Membership's
// tables itself.
//
// ONE INDEXED READ PER AUTHENTICATED REQUEST, AND DELIBERATELY NOT CACHED. A cached answer
// reintroduces exactly the staleness window this exists to close — a 30-second cache would make
// AC-090 true "within 30 seconds", which is the same shape of almost-true the 5-minute token gave
// us. The system is bounded at <=20 users and low traffic (CON/NFR), so the read is affordable; if
// that ever stops being true the fix is a cache keyed on an INVALIDATION SIGNAL, not a TTL.
//
// AsNoTracking + a projection: this runs before MVC/MediatR on every request and must not drag an
// aggregate into the change tracker, where it would be a candidate for an accidental save.
public sealed class PrincipalRevalidator : IPrincipalRevalidator
{
    private readonly IMembershipDbContext _db;
    private readonly IClock _clock;

    public PrincipalRevalidator(IMembershipDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<PrincipalVerdict> RevalidateAsync(
        string keycloakUserId, DateTimeOffset issuedAt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keycloakUserId))
            return PrincipalVerdict.Allowed; // no subject to check — token validation already ran

        var row = await _db.Members.AsNoTracking()
            .Where(m => m.KeycloakUserId == keycloakUserId)
            .Select(m => new { m.Status, m.RolesChangedAt, m.AccessExpiresAt })
            .FirstOrDefaultAsync(ct);

        // ⚠ NO ROW = ALLOWED, and this is load-bearing rather than lenient. ADR-0004 provisions the
        // local profile JUST IN TIME on first login, so a valid token legitimately exists before its
        // member row does — the SPA calls POST /members/me to create it. Refusing here would refuse
        // every first login in the system, on the path of every request. See IPrincipalRevalidator.
        if (row is null)
            return PrincipalVerdict.Allowed;

        if (row.Status == MembershipStatus.Disabled)
            return PrincipalVerdict.Disabled;

        // AC-092. Checked BEFORE staleness: an expired guest must be told their access ended rather
        // than sent to renew a token, and a member can plausibly be both.
        if (row.AccessExpiresAt is { } expiresAt && _clock.UtcNow > expiresAt)
            return PrincipalVerdict.Expired;

        // AC-090. `iat` has one-second resolution, so a role change in the SAME second the token was
        // issued is genuinely ambiguous — the tolerance resolves it toward REFUSING, because
        // refusing costs one silent renewal and admitting costs a stale privilege.
        //
        // ⚠ THIS IS THE LINE THAT COULD LOOP. A refused token sends the SPA to renew
        // (automaticSilentRenew), and the renewal only helps if the new token's `iat` lands AFTER
        // RolesChangedAt. It does, because RolesChangedAt is stamped from THIS application's clock
        // (IClock, passed into ApplyAssignedRole) while `iat` comes from Keycloak's — and they are
        // the same host in every ACMP topology. If they ever are not, a skew where Keycloak's clock
        // trails the API's by more than a token lifetime turns this into an unbreakable renewal
        // loop for the affected member, which is why the tolerance is one second and not one minute.
        if (row.RolesChangedAt is { } changedAt && changedAt >= issuedAt.AddSeconds(-1))
            return PrincipalVerdict.Stale;

        return PrincipalVerdict.Allowed;
    }
}
