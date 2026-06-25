using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Acmp.Shared.Authorization;

// Maps Keycloak claim strings to canonical roles. Mirrors the SPA's roles.ts normalization so the
// client and server resolve identical roles: a claim may arrive bare ("chairman"), ACMP-prefixed
// ("acmp-chairman"), or as a group path ("/acmp/chairman"); "coordinator" is the legacy alias for
// Secretary (renamed 2026-06-25). Config overrides (RoleMappingOptions.ClaimToRole) win first.
public sealed partial class KeycloakRoleClaimMapper : IRoleClaimMapper
{
    private static readonly Dictionary<string, string> Canonical =
        AcmpRoles.All.ToDictionary(r => r.ToLowerInvariant(), r => r);

    private static readonly Dictionary<string, string> Aliases =
        new(StringComparer.OrdinalIgnoreCase) { ["coordinator"] = AcmpRoles.Secretary };

    private readonly RoleMappingOptions _options;

    public KeycloakRoleClaimMapper(IOptions<RoleMappingOptions> options) => _options = options.Value;

    public IReadOnlyCollection<string> Map(IEnumerable<string> rawClaims)
    {
        var roles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in rawClaims)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var role = Resolve(raw.Trim());
            if (role is not null) roles.Add(role);
        }
        return roles;
    }

    private string? Resolve(string raw)
    {
        if (_options.ClaimToRole.TryGetValue(raw, out var configured) && IsCanonical(configured))
            return configured;

        var leaf = LeafPattern().Replace(raw.ToLowerInvariant(), string.Empty);
        var slash = leaf.LastIndexOf('/');
        if (slash >= 0) leaf = leaf[(slash + 1)..];

        if (Canonical.TryGetValue(leaf, out var canonical)) return canonical;
        return Aliases.TryGetValue(leaf, out var alias) ? alias : null;
    }

    private static bool IsCanonical(string role) => Canonical.ContainsValue(role);

    // Strips a leading "/", "acmp/" or "acmp-" prefix; the trailing group segment is taken after.
    [GeneratedRegex("^/?(acmp[/-])?")]
    private static partial Regex LeafPattern();
}
