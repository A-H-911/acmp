namespace Acmp.Shared.Authorization;

// Canonical global role names (README §C / docs/domain/permission-role-matrix.md §B). These string codes are the single
// authorization vocabulary: token role claims map TO them (IRoleClaimMapper), ASP.NET policies
// require them, and Membership's CommitteeRole enum members are named to match (nameof aligns).
// Guest/Presenter is the single global role "Guest"; the Presenter ability is the per-topic
// relationship (docs/domain/permission-role-matrix.md §D), not a separate role.
public static class AcmpRoles
{
    public const string Chairman = "Chairman";
    public const string Secretary = "Secretary";
    public const string Member = "Member";
    public const string Reviewer = "Reviewer";
    public const string Auditor = "Auditor";
    public const string Administrator = "Administrator";
    public const string Submitter = "Submitter";
    public const string Guest = "Guest";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Chairman, Secretary, Member, Reviewer, Auditor, Administrator, Submitter, Guest,
    };

    /// <summary>
    /// True when the principal holds Guest and NO committee role — the external presenter ADR-0040
    /// introduces, as opposed to an insider who happens to be listed as a guest somewhere.
    /// </summary>
    /// <param name="isInRole">
    /// Role membership for the caller: <c>ClaimsPrincipal.IsInRole</c> at the HTTP edge,
    /// <c>ICurrentUser.IsInRole</c> inside a handler. Taken as a delegate so this predicate can live
    /// in the shared role vocabulary without depending on either of them.
    /// </param>
    /// <remarks>
    /// DEFINED ONCE BECAUSE TWO COPIES WOULD DRIFT. GuestSurfaceMiddleware gates the request SURFACE
    /// with this and the Meetings read handlers scope their ROWS with it; while the middleware owned a
    /// private copy, the gate believed it had closed a read the handlers still served committee-wide
    /// (DEF-073). Roles are Keycloak claims and cannot be self-assigned, so holding a committee role
    /// alongside Guest is an insider signal rather than an escape hatch.
    /// </remarks>
    public static bool IsGuestOnly(Func<string, bool> isInRole) =>
        isInRole(Guest) && !All.Any(role => role != Guest && isInRole(role));
}
