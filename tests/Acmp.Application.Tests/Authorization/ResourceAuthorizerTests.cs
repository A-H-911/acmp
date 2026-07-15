using System.Security.Claims;
using Acmp.Shared.Application.Exceptions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Acmp.Application.Tests.Authorization;

// Handler-level resource authorization seam (ResourceAuthorizer) — reuses IAuthorizationService so the
// same policy handlers used by endpoints gate resource decisions. Covers the deny (403) and no-principal
// (401) guards that endpoint tests don't reach directly.
[Trait("Category", "Security")]
public class ResourceAuthorizerTests
{
    private const string Policy = "SomePolicy";

    private static ClaimsPrincipal User() =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "u") }, "Test"));

    private static (ResourceAuthorizer sut, IAuthorizationService authz) Build(ClaimsPrincipal? user)
    {
        var authz = Substitute.For<IAuthorizationService>();
        var http = Substitute.For<IHttpContextAccessor>();
        http.HttpContext.Returns(user is null ? null : new DefaultHttpContext { User = user });
        return (new ResourceAuthorizer(authz, http), authz);
    }

    [Fact]
    public async Task EnsureAsync_throws_forbidden_when_the_policy_denies()
    {
        var user = User();
        var (sut, authz) = Build(user);
        var resource = new object();
        authz.AuthorizeAsync(user, resource, Policy).Returns(AuthorizationResult.Failed());

        await sut.Invoking(s => s.EnsureAsync(resource, Policy))
            .Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task EnsureAsync_does_not_throw_when_the_policy_grants()
    {
        var user = User();
        var (sut, authz) = Build(user);
        var resource = new object();
        authz.AuthorizeAsync(user, resource, Policy).Returns(AuthorizationResult.Success());

        await sut.Invoking(s => s.EnsureAsync(resource, Policy)).Should().NotThrowAsync();
    }

    [Fact]
    public async Task IsAuthorizedAsync_throws_unauthorized_when_there_is_no_principal()
    {
        var (sut, _) = Build(user: null);

        await sut.Invoking(s => s.IsAuthorizedAsync(new object(), Policy))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }
}

// Config-bound Keycloak claim -> role mapping options (ADR-0004). DefaultRole governs the fail-closed
// "validated token with no recognised role" path.
public class RoleMappingOptionsTests
{
    [Fact]
    public void DefaultRole_defaults_to_null_and_round_trips()
    {
        var options = new RoleMappingOptions();

        options.DefaultRole.Should().BeNull();

        options.DefaultRole = "Submitter";
        options.DefaultRole.Should().Be("Submitter");
    }
}
