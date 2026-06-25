using System.Security.Claims;
using System.Text.Json;

namespace Acmp.Shared.Authorization;

// Gathers candidate role/group strings from a Keycloak access token so IRoleClaimMapper can resolve
// them to canonical roles. Handles the nested-JSON claims Keycloak emits — realm_access.roles and
// resource_access.{client}.roles — plus the flat "groups"/"roles"/role claims. Extracted from the
// JwtBearer event wiring so the parsing is unit-testable independently of the host.
public static class KeycloakClaims
{
    public static IReadOnlyCollection<string> RoleValues(ClaimsPrincipal principal)
    {
        var values = new List<string>();
        foreach (var claim in principal.Claims)
        {
            switch (claim.Type)
            {
                case "groups":
                case "roles":
                case "role":
                case ClaimTypes.Role:
                    values.Add(claim.Value);
                    break;
                case "realm_access":
                case "resource_access":
                    values.AddRange(ExtractJsonRoles(claim.Value));
                    break;
            }
        }
        return values;
    }

    private static IEnumerable<string> ExtractJsonRoles(string json)
    {
        var result = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            CollectRoles(doc.RootElement, result);
        }
        catch (JsonException)
        {
            // A flat (non-JSON) claim value of this name — ignore; flat roles are read elsewhere.
        }
        return result;
    }

    private static void CollectRoles(JsonElement element, List<string> sink)
    {
        if (element.ValueKind != JsonValueKind.Object) return;
        foreach (var prop in element.EnumerateObject())
        {
            if (prop.NameEquals("roles") && prop.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var role in prop.Value.EnumerateArray())
                    if (role.ValueKind == JsonValueKind.String)
                        sink.Add(role.GetString()!);
            }
            else
            {
                CollectRoles(prop.Value, sink);
            }
        }
    }
}
