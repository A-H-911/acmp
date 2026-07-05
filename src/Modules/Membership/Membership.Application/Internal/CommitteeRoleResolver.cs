using Acmp.Modules.Membership.Domain.Enums;

namespace Acmp.Modules.Membership.Application.Internal;

// Resolves the single cached "primary" role from the set of canonical role names a principal holds
// (a principal may hold several, docs/domain/permission-role-matrix.md §B). Highest privilege wins — the CommitteeRole enum is
// ordered most-privileged first (Chairman = 0), so the lowest value is primary.
internal static class CommitteeRoleResolver
{
    public static CommitteeRole? PrimaryRole(IEnumerable<string> roleNames)
    {
        CommitteeRole? primary = null;
        foreach (var name in roleNames)
        {
            if (Enum.TryParse<CommitteeRole>(name, ignoreCase: true, out var role) &&
                (primary is null || (int)role < (int)primary))
            {
                primary = role;
            }
        }
        return primary;
    }
}
