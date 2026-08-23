namespace Acmp.Shared.Contracts.Membership;

// Cross-module WRITE port (ADR-0021 pattern; DW-025, authorized by DEC-041): the module that owns a
// meeting can move or close the access windows of the guests presenting at it, without reaching into
// Membership's tables.
//
// ⚠ SEPARATE FROM IGuestProvisioner ON PURPOSE, and the reason is availability rather than tidiness.
// IGuestProvisioner is registered ONLY when the Keycloak admin client is configured, because it
// cannot create an account without one. This port creates nothing — it is a pure ACMP-side column
// write — so it is registered UNCONDITIONALLY, exactly like IPrincipalRevalidator. Folding it into
// the provisioner would mean CANCELLING A MEETING FAILS whenever in-app user management is switched
// off, which is every environment today.
//
// Unauthorized at the port, per ADR-0021: the calling action (cancel a meeting, change a presenter)
// is separately authorized, and a second check here would be a second place for the answer to drift.
public interface IGuestWindowWriter
{
    /// <summary>
    /// Sets the access window of the given members to <paramref name="expiresAt"/>, and returns how
    /// many rows actually changed.
    /// </summary>
    /// <remarks>
    /// ⚠ ONLY MEMBERS WHO ALREADY HOLD A WINDOW ARE TOUCHED. An ordinary committee member has a null
    /// AccessExpiresAt and must keep it — giving one an expiry because they happened to present at a
    /// cancelled meeting would lock a real member out of the whole product. That guard lives HERE,
    /// in the one implementation, rather than in each caller, because a caller that forgot it would
    /// fail silently and permanently (DEF-029: the member row can be disabled but never deleted).
    ///
    /// Passing "now" CLOSES the window: the per-request refusal (ADR-0039) is exclusive of the
    /// instant itself, so access stops on the very next request.
    /// </remarks>
    Task<int> SetGuestWindowsAsync(
        IReadOnlyCollection<Guid> memberIds, DateTimeOffset expiresAt, CancellationToken ct = default);
}
