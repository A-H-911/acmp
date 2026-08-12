namespace Acmp.Shared.Contracts.Membership;

/// <summary>
/// ADR-0039 — the per-request veto. Authorization in ACMP is otherwise ENTIRELY token-driven
/// (CurrentUserService.IsInRole reads the ClaimsPrincipal), so a validated token is honoured until
/// it expires no matter what has happened since: a role removed through the app lingers for a full
/// access-token lifetime (measured at 300s on the live realm), and a guest whose window has closed
/// keeps their access. Keycloak's forced sign-out ends the SESSION; it cannot revoke a token in
/// flight.
///
/// This is the cross-module seam (ADR-0001): the API host asks Membership whether the principal may
/// still act rather than reading Membership's tables. Implemented in Membership.Infrastructure,
/// exactly like <see cref="ICommitteeDirectory"/>.
///
/// It is a LOCAL read, deliberately not Keycloak token introspection — introspection would put a
/// network call on every request and make Keycloak's availability a precondition for serving
/// anything, where the API currently validates JWTs offline against cached JWKS and keeps working
/// while Keycloak restarts. The state that matters here is ACMP's own.
/// </summary>
public interface IPrincipalRevalidator
{
    /// <param name="keycloakUserId">The token's subject.</param>
    /// <param name="issuedAt">The token's <c>iat</c>. A token minted before a role change is stale.</param>
    Task<PrincipalVerdict> RevalidateAsync(string keycloakUserId, DateTimeOffset issuedAt, CancellationToken ct = default);
}

/// <summary>
/// Why a principal was refused, or <see cref="Allowed"/>. A REASON rather than a bool because the
/// correct client behaviour differs: a stale token should be renewed (the SPA already runs
/// automaticSilentRenew), whereas an ended window or a disabled account must not retry at all —
/// answering both with the same 401 would send an expired guest into a silent renewal loop.
/// </summary>
public enum PrincipalVerdict
{
    /// <summary>Act. Also the answer when no member row exists yet — see the note below.</summary>
    Allowed = 0,

    /// <summary>
    /// Roles changed after this token was issued. The token is not forged, it is OUT OF DATE, and a
    /// renewal fixes it — which is precisely what makes AC-090 literally true rather than true
    /// within five minutes.
    /// </summary>
    Stale = 1,

    /// <summary>The member's access window has closed (AC-092). Renewal will not help.</summary>
    Expired = 2,

    /// <summary>The member is disabled (AC-058). Renewal will not help.</summary>
    Disabled = 3,
}

/*
 * ⚠ WHY "NO MEMBER ROW" MUST BE Allowed, AND WHY IT IS THE MOST DANGEROUS LINE IN THIS FEATURE.
 *
 * ADR-0004 provisions the local profile JUST IN TIME on first authenticated login, so a perfectly
 * valid token legitimately exists BEFORE its CommitteeMember row does — the SPA calls POST
 * /members/me to create it. A fail-closed check that refused an unknown subject would refuse every
 * first login in the system, including the operator's, and it would do so on the path of every
 * request. That is the "new way to lock everyone out" ADR-0039 records as its worst consequence.
 *
 * Allowing an unknown subject is safe because the token is already cryptographically valid and
 * carries its own roles; this check narrows what a valid token may do, it does not replace token
 * validation. Anything that must not be reachable without a member row is the endpoint's own
 * business — and non-integration actors reach exactly one such endpoint before provisioning.
 */
