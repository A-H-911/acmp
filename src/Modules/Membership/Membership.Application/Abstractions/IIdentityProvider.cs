namespace Acmp.Modules.Membership.Application.Abstractions;

// The Membership module's port onto the identity provider (ADR-0038). KEYCLOAK REMAINS THE SOURCE
// OF TRUTH FOR IDENTITY; ACMP writes THROUGH to it and never becomes a second identity store.
//
// This is the application's FIRST outbound WRITE to Keycloak — before ADR-0038 there were zero
// callers of its Admin API anywhere in src/. That is why the surface here is deliberately narrow:
// FOUR WRITES PLUS ONE BOUNDED READ, no general-purpose "call Keycloak" escape hatch, so the blast
// radius of the service-account credential is bounded by this interface rather than by whatever a
// caller invents.
//
// ⚠ THE READ IS A DELIBERATE WIDENING, RECORDED AS SC-011 BEFORE IT WAS WRITTEN. Until then every
// operation here was a write, and that was the whole shape of the guarantee. DEC-046 chose to
// reconcile Keycloak accounts into committee_members ahead of the stream-scope deploy (DEF-065), and
// no combination of the four writes can enumerate anything — so listing is the one capability the
// feature needs and the port lacked. It stays defensible for a reason that does not generalise to a
// second widening: a READ cannot mutate the identity provider, so the blast-radius argument the
// narrowness protects is untouched in the direction it was written to protect. A fifth WRITE would
// need its own decision.
//
// Owned by Membership and implemented in Membership.Infrastructure, so ADR-0001's module boundary
// holds and no other module acquires a Keycloak dependency.
public interface IIdentityProvider
{
    /// <summary>
    /// Creates an account and returns (subject id, temporary password).
    /// </summary>
    /// <remarks>
    /// The password is generated here, marked must-change-at-first-login, and returned ONCE. It is
    /// never persisted, logged or written to a file — "no email in v1" is a hard constraint, so the
    /// only delivery channel is showing it to the inviter, and the 26-password CSV that had to be
    /// deleted by hand is the hazard this must not repeat (AC-088).
    /// </remarks>
    Task<InvitedAccount> CreateUserAsync(string email, string fullName, CancellationToken ct = default);

    /// <summary>Replaces the account's realm roles with exactly <paramref name="roles"/>.</summary>
    Task SetRealmRolesAsync(string subjectId, IReadOnlyCollection<string> roles, CancellationToken ct = default);

    /// <summary>
    /// Terminates every active session for the account, so a role change takes effect on the next
    /// request instead of lingering until the 60-minute idle timeout (AC-090).
    /// </summary>
    /// <remarks>
    /// Roles reach the app through the token and are cached on CommitteeMember at login, so without
    /// this a REMOVED role stays usable for up to an hour — the half of revocation that matters.
    /// </remarks>
    Task SignOutEverywhereAsync(string subjectId, CancellationToken ct = default);

    /// <summary>Disables the account so the login itself stops working.</summary>
    /// <remarks>
    /// Guest expiry is enforced ACMP-side on every request; this is defence in depth on top of it,
    /// not the enforcement (AC-092). Disable, never delete — deleting a Keycloak user strands its
    /// member row forever (DEF-029), and it is what produced the duplicate identities that made six
    /// e2e tests fail against UAT (DEF-045).
    /// </remarks>
    Task DisableUserAsync(string subjectId, CancellationToken ct = default);

    /// <summary>
    /// Every account in the realm, with the realm roles each one holds (SC-011).
    /// </summary>
    /// <remarks>
    /// The ONE read on this port, and it exists for exactly one caller: the DEC-046 reconciliation
    /// that gives the accounts seeded straight into Keycloak a committee_members row before stream
    /// scope starts refusing them (DEF-065, DEF-071). It is a full listing rather than a lookup
    /// because the reconciliation's question is "which accounts have no row", which nothing narrower
    /// can answer.
    /// </remarks>
    Task<IReadOnlyList<IdentityAccount>> ListUsersAsync(CancellationToken ct = default);
}

/// <summary>An account just created in the identity provider. <paramref name="TemporaryPassword"/> is revealed once and never stored.</summary>
public sealed record InvitedAccount(string SubjectId, string TemporaryPassword);

/// <summary>An account that already exists in the identity provider, as the reconciliation sees it.</summary>
/// <param name="SubjectId">The OIDC subject — the same value CommitteeMember.KeycloakUserId stores.</param>
/// <param name="Enabled">
/// False for an account that cannot sign in. Carried rather than filtered out at the source so the
/// caller can REPORT the skip: a reconciliation that silently ignores accounts is indistinguishable
/// from one that failed to see them.
/// </param>
/// <param name="RealmRoles">
/// Raw realm role names, unmapped. Keycloak's own composites (default-roles-*) appear here too;
/// resolving which of them is a committee role is the application's job, not the port's.
/// </param>
public sealed record IdentityAccount(
    string SubjectId, string Email, string FullName, bool Enabled, IReadOnlyCollection<string> RealmRoles);
