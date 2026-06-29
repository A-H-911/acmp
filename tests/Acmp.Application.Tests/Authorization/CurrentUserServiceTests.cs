using System.Security.Claims;
using Acmp.Shared.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Acmp.Application.Tests.Authorization;

// Exercises the uncovered branches in CurrentUserService:
//   - null HttpContext → IsAuthenticated false, Roles empty, IsInRole false
//   - "sub" claim fallback for UserId when NameIdentifier is absent
//   - Identity.Name fallback for UserName when preferred_username is absent
//   - UserName fallback for DisplayName when "name" claim is absent
public class CurrentUserServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static CurrentUserService ForPrincipal(ClaimsPrincipal principal)
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        var ctx = new DefaultHttpContext { User = principal };
        accessor.HttpContext.Returns(ctx);
        return new CurrentUserService(accessor);
    }

    private static CurrentUserService WithNullContext()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        return new CurrentUserService(accessor);
    }

    // ClaimsIdentity with "Bearer" auth type so IsAuthenticated → true.
    // RoleClaimType defaults to ClaimTypes.Role when not overridden.
    private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "Bearer"));

    // ── Null HttpContext paths ────────────────────────────────────────────────

    [Fact]
    public void IsAuthenticated_returns_false_when_HttpContext_is_null()
    {
        // Arrange  ← `User?.Identity?.IsAuthenticated ?? false` null branch
        var svc = WithNullContext();

        // Assert
        svc.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void Roles_returns_empty_when_HttpContext_is_null()
    {
        // Arrange  ← `User?.FindAll(...) ?? Array.Empty<string>()` null branch
        var svc = WithNullContext();

        // Assert
        svc.Roles.Should().BeEmpty();
    }

    [Fact]
    public void IsInRole_returns_false_when_HttpContext_is_null()
    {
        // Arrange  ← `User?.IsInRole(role) ?? false` null branch
        var svc = WithNullContext();

        // Assert
        svc.IsInRole("Chairman").Should().BeFalse();
    }

    // ── UserId claim fallbacks ────────────────────────────────────────────────

    [Fact]
    public void UserId_resolves_from_NameIdentifier_claim_when_present()
    {
        // Arrange — primary path (already covered baseline, included for completeness)
        var svc = ForPrincipal(Authenticated(
            new Claim(ClaimTypes.NameIdentifier, "kc-primary")));

        // Assert
        svc.UserId.Should().Be("kc-primary");
    }

    [Fact]
    public void UserId_falls_back_to_sub_claim_when_NameIdentifier_is_absent()
    {
        // Arrange  ← `?? User?.FindFirst("sub")?.Value` fallback was uncovered
        var svc = ForPrincipal(Authenticated(
            new Claim("sub", "kc-sub-fallback")));

        // Assert
        svc.UserId.Should().Be("kc-sub-fallback");
    }

    [Fact]
    public void UserId_returns_null_when_neither_NameIdentifier_nor_sub_is_present()
    {
        // Arrange
        var svc = ForPrincipal(Authenticated(
            new Claim("email", "user@example.com")));

        // Assert
        svc.UserId.Should().BeNull();
    }

    // ── UserName claim fallbacks ──────────────────────────────────────────────

    [Fact]
    public void UserName_resolves_from_preferred_username_claim_when_present()
    {
        // Arrange — primary path
        var svc = ForPrincipal(Authenticated(
            new Claim("preferred_username", "a.hammo")));

        // Assert
        svc.UserName.Should().Be("a.hammo");
    }

    [Fact]
    public void UserName_falls_back_to_Identity_Name_when_preferred_username_is_absent()
    {
        // Arrange  ← `?? User?.Identity?.Name` fallback was uncovered.
        // ClaimsIdentity sets Identity.Name from the NameClaimType claim
        // (defaults to ClaimTypes.Name = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name").
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "Fallback Name") },
            "Bearer");
        var svc = ForPrincipal(new ClaimsPrincipal(identity));

        // Assert
        svc.UserName.Should().Be("Fallback Name");
    }

    [Fact]
    public void UserName_returns_null_when_neither_claim_is_present()
    {
        // Arrange
        var svc = ForPrincipal(Authenticated(
            new Claim("email", "user@example.com")));

        // Assert
        svc.UserName.Should().BeNull();
    }

    // ── DisplayName claim fallback ────────────────────────────────────────────

    [Fact]
    public void DisplayName_resolves_from_name_claim_when_present()
    {
        // Arrange — primary path
        var svc = ForPrincipal(Authenticated(
            new Claim("name", "Anas Hammo"),
            new Claim("preferred_username", "a.hammo")));

        // Assert
        svc.DisplayName.Should().Be("Anas Hammo");
    }

    [Fact]
    public void DisplayName_falls_back_to_UserName_when_name_claim_is_absent()
    {
        // Arrange  ← `?? UserName` fallback was uncovered
        var svc = ForPrincipal(Authenticated(
            new Claim("preferred_username", "a.hammo")));

        // Assert — no "name" claim, so DisplayName == UserName == preferred_username
        svc.DisplayName.Should().Be("a.hammo");
    }

    // ── Roles and IsInRole (happy paths) ─────────────────────────────────────

    [Fact]
    public void Roles_returns_all_role_claims_as_a_collection()
    {
        // Arrange
        var svc = ForPrincipal(Authenticated(
            new Claim(ClaimTypes.Role, "Chairman"),
            new Claim(ClaimTypes.Role, "Secretary")));

        // Assert
        svc.Roles.Should().BeEquivalentTo("Chairman", "Secretary");
    }

    [Fact]
    public void IsInRole_returns_true_when_principal_has_the_role()
    {
        // Arrange
        var svc = ForPrincipal(Authenticated(
            new Claim(ClaimTypes.Role, "Member")));

        // Assert
        svc.IsInRole("Member").Should().BeTrue();
        svc.IsInRole("Chairman").Should().BeFalse();
    }
}
