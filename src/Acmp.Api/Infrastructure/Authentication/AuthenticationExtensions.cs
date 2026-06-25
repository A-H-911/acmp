using System.Security.Claims;
using System.Text.Json;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Acmp.Api.Infrastructure.Authentication;

// Keycloak OIDC bearer authentication (ADR-0004). Config-driven from "Authentication:Keycloak";
// token validation is local (signature + issuer + audience). On a valid token, OnTokenValidated
// maps Keycloak realm/group role claims to canonical ACMP role claims (IRoleClaimMapper) so policy
// authorization and ICurrentUser.IsInRole work off one vocabulary. With no Authority configured the
// scheme rejects every token and protected endpoints return 401 — fail-closed (AC-008).
public static class AuthenticationExtensions
{
    public static IServiceCollection AddAcmpAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("Authentication:Keycloak");
        var authority = section["Authority"];
        var audience = section["Audience"];

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata = section.GetValue("RequireHttpsMetadata", true);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = !string.IsNullOrWhiteSpace(authority),
                    ValidateAudience = !string.IsNullOrWhiteSpace(audience),
                    ValidAudience = audience,
                    NameClaimType = "preferred_username",
                    RoleClaimType = ClaimTypes.Role,
                };
                options.Events = new JwtBearerEvents { OnTokenValidated = MapKeycloakRolesAsync };
            });

        return services;
    }

    private static async Task MapKeycloakRolesAsync(TokenValidatedContext context)
    {
        var services = context.HttpContext.RequestServices;
        var mapper = services.GetRequiredService<IRoleClaimMapper>();
        var options = services.GetRequiredService<IOptions<RoleMappingOptions>>().Value;
        var principal = context.Principal!;
        var identity = (ClaimsIdentity)principal.Identity!;

        var roles = mapper.Map(ExtractRawRoleClaims(principal));

        if (roles.Count == 0)
        {
            // AC-003: validated token carries no recognised ACMP role claim. Emit an AuthEvent and
            // apply the configured default (deny when null — fail-closed).
            var audit = services.GetRequiredService<IAuditSink>();
            var subject = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("sub")?.Value;
            await audit.EmitAsync("Authentication.NoRoleClaim", subject, new { defaultRole = options.DefaultRole });

            if (!string.IsNullOrWhiteSpace(options.DefaultRole))
                identity.AddClaim(new Claim(ClaimTypes.Role, options.DefaultRole));
            return;
        }

        foreach (var role in roles)
            if (!principal.IsInRole(role))
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
    }

    // Gathers candidate role/group strings from a Keycloak token: realm_access.roles and
    // resource_access.{client}.roles (nested JSON claims), the "groups" claim, and flat role claims.
    private static IEnumerable<string> ExtractRawRoleClaims(ClaimsPrincipal principal)
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
