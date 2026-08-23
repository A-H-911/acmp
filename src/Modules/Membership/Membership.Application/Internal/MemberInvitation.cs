using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Membership.Application.Internal;

// The one place an invited account is created (FR-156 / AC-088 and FR-159 / AC-092 both land here).
//
// PUBLIC, unlike the other Internal/ helpers, and the reason is deliberate: the second caller is this
// module's OWN cross-module port implementation (GuestProvisioner), which ADR-0021 requires to live in
// Membership.Infrastructure — a different assembly. The alternative was copying twenty lines whose
// ORDER is the safety property, into a second file, where a later fix to one would silently miss the
// other. Nothing outside the Membership module has a reason to call it.
public static class MemberInvitation
{
    /// <summary>
    /// Creates the Keycloak account and the local <c>Invited</c> member, and audits it.
    /// </summary>
    /// <param name="accessExpiresAt">Null for an ordinary member; set for a time-boxed guest (FR-159).</param>
    /// <param name="auditAction">The governance verb — the two callers are different events.</param>
    /// <param name="realmRoles">
    /// Realm roles to grant the new account, or null to leave it inert (SC-012 / DEF-076).
    /// </param>
    /// <remarks>
    /// ⚠ THE TWO CALLERS ARE DELIBERATELY ASYMMETRIC HERE, AND THAT IS THE WHOLE POINT OF SC-012.
    /// FR-156's InviteUser passes NOTHING: an invited member is inert until an administrator decides
    /// their role through AssignRoles, which is ADR-0038's two-step flow. FR-159's guest presenter
    /// passes Guest, because DEC-037's premise is that the Secretary's single action from the meeting
    /// screen IS the whole use case — a guest has no follow-up actor to grant them anything later.
    ///
    /// Until DEF-076 this parameter did not exist and BOTH paths were inert, so a guest presenter's
    /// token satisfied nothing: GetMySessionQuery refused them from their own session, and
    /// GuestSurfaceMiddleware did not even classify them as a guest, because IsGuestOnly reads the
    /// TOKEN while committee_members said Guest. Do not "tidy" the two callers back into one uniform
    /// behaviour — whichever way that merged, it would either restore the defect or strip FR-156's
    /// inert-invite property.
    /// </remarks>
    public static async Task<(CommitteeMember Member, string TemporaryPassword)> InviteAsync(
        IMembershipDbContext db, IIdentityProvider identity, IAuditSink audit, IClock clock,
        string email, string fullName, CommitteeRole role, DateTimeOffset? accessExpiresAt,
        string auditAction, CancellationToken ct, IReadOnlyCollection<string>? realmRoles = null)
    {
        var normalized = email.Trim().ToLowerInvariant();

        // Refuse a duplicate BEFORE touching Keycloak. Email is uniquely indexed where non-empty, so
        // the insert would fail anyway — but only AFTER the account existed in Keycloak, leaving a
        // real user behind for a request that reported failure.
        if (await db.Members.AnyAsync(m => m.Email == normalized, ct))
            throw new InvalidOperationException($"A member with the email {normalized} already exists.");

        var account = await identity.CreateUserAsync(normalized, fullName, ct);

        // BEFORE THE LOCAL ROW, AND THE ORDER IS THE SAFETY PROPERTY AGAIN (DEF-076 / SC-012). If this
        // fails, what is left is a Keycloak account with no role and NO member row — which JIT
        // provisioning handles on first login exactly as it already does for the insert failure below.
        // Assigning AFTER the insert would leave the opposite: a member row that says Guest with a
        // timed access window, over a token that says nobody. That is precisely the state DEF-076
        // describes, and it is unrecoverable without a second manual step, because nothing re-reads the
        // row to re-derive the grant.
        if (realmRoles is { Count: > 0 })
            await identity.SetRealmRolesAsync(account.SubjectId, realmRoles, ct);

        // Status=Invited with the subject id Keycloak just returned. First login flips it to Active
        // through the existing SyncFromClaims path (SC-003) — there is no second creation path and
        // nothing to reconcile, because the identity is known here rather than guessed.
        //
        // ORDER MATTERS AND THE FAILURE MODE IS DELIBERATE: if this insert fails after the account
        // exists, what is left is a Keycloak user with NO member row, which JIT provisioning creates
        // on first login (ADR-0004). The permanent damage in this system lives in the member row —
        // DEF-029 means it can be disabled but never deleted — and no member row was written.
        var member = CommitteeMember.PreRegister(account.SubjectId, fullName, normalized, role, clock.UtcNow);
        member.SetAccessWindow(accessExpiresAt);
        db.Members.Add(member);
        await db.SaveChangesAsync(ct);

        await audit.EmitEnrichedAsync(auditAction, nameof(CommitteeMember), member.PublicId.ToString(), ct: ct);

        return (member, account.TemporaryPassword);
    }
}
