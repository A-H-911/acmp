using System.Security.Claims;
using Acmp.Api.Infrastructure.Authentication;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Acmp.Api.Tests;

// Exercises the JwtBearer OnTokenValidated event wired by AddAcmpAuthentication (MapKeycloakRolesAsync):
// the Keycloak realm/group role claims -> canonical ACMP role mapping, including the fail-closed
// no-recognised-role branch (AC-003). We resolve the options the extension produces and invoke the
// configured event delegate directly with a hand-built context.
public class AuthenticationExtensionsTests
{
    private static ClaimsIdentity Invoke(
        IReadOnlyCollection<string> mappedRoles, string? defaultRole, out IAuditSink audit, params Claim[] seedClaims)
    {
        var mapper = Substitute.For<IRoleClaimMapper>();
        mapper.Map(Arg.Any<IEnumerable<string>>()).Returns(mappedRoles);
        audit = Substitute.For<IAuditSink>();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(mapper);
        services.AddSingleton(audit);
        services.Configure<RoleMappingOptions>(o => o.DefaultRole = defaultRole);
        services.AddAcmpAuthentication(new ConfigurationBuilder().Build());
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        var identity = new ClaimsIdentity(seedClaims, "jwt");
        var principal = new ClaimsPrincipal(identity);
        var http = new DefaultHttpContext { RequestServices = provider };
        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var context = new TokenValidatedContext(http, scheme, options) { Principal = principal };

        options.Events!.OnTokenValidated(context).GetAwaiter().GetResult();
        return identity;
    }

    private static IReadOnlyList<string> RolesOf(ClaimsIdentity identity) =>
        identity.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

    [Fact]
    public async Task No_recognised_role_and_no_default_emits_audit_and_grants_nothing()
    {
        var identity = Invoke(
            Array.Empty<string>(), defaultRole: null, out var audit,
            new Claim(ClaimTypes.NameIdentifier, "kc-1"));

        RolesOf(identity).Should().BeEmpty();
        await audit.Received(1).EmitAsync(
            "Authentication.NoRoleClaim", "kc-1", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task No_recognised_role_with_a_configured_default_assigns_that_role()
    {
        var identity = Invoke(
            Array.Empty<string>(), defaultRole: "Submitter", out var audit,
            new Claim(ClaimTypes.NameIdentifier, "kc-2"));

        RolesOf(identity).Should().ContainSingle().Which.Should().Be("Submitter");
        await audit.Received(1).EmitAsync(
            "Authentication.NoRoleClaim", "kc-2", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Mapped_roles_are_added_once_without_duplicating_a_role_already_held()
    {
        // Chairman already present on the token -> not re-added (line 67 false); Secretary added.
        var identity = Invoke(
            new[] { AcmpRoles.Chairman, AcmpRoles.Secretary }, defaultRole: null, out _,
            new Claim(ClaimTypes.NameIdentifier, "kc-3"),
            new Claim(ClaimTypes.Role, AcmpRoles.Chairman));

        RolesOf(identity).Should().BeEquivalentTo(new[] { AcmpRoles.Chairman, AcmpRoles.Secretary });
    }
}
