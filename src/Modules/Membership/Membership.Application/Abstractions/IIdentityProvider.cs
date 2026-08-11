namespace Acmp.Modules.Membership.Application.Abstractions;

// The Membership module's port onto the identity provider (ADR-0038). KEYCLOAK REMAINS THE SOURCE
// OF TRUTH FOR IDENTITY; ACMP writes THROUGH to it and never becomes a second identity store.
//
// This is the application's FIRST outbound WRITE to Keycloak — before ADR-0038 there were zero
// callers of its Admin API anywhere in src/. That is why the surface here is deliberately narrow:
// four operations, no general-purpose "call Keycloak" escape hatch, so the blast radius of the
// service-account credential is bounded by this interface rather than by whatever a caller invents.
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
}

/// <summary>An account just created in the identity provider. <paramref name="TemporaryPassword"/> is revealed once and never stored.</summary>
public sealed record InvitedAccount(string SubjectId, string TemporaryPassword);
