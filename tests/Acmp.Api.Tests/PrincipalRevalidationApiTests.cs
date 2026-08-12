using System.Net;
using Acmp.Modules.Membership.Domain.Enums;
using FluentAssertions;

namespace Acmp.Api.Tests;

// ADR-0039 / AC-090 / AC-092 — the refusal, FORCED THROUGH THE REAL PIPELINE.
//
// THIS FILE IS THE REASON THE ENFORCEMENT POINT IS A MIDDLEWARE AND NOT JwtBearerEvents (SC-005).
// AC-090's bar is "the user's SUBSEQUENT REQUEST no longer exercises the removed role, not merely
// that a logout call was issued" — so the proof has to be a real HTTP request that is refused. A
// JwtBearer event cannot run here at all: this host authenticates with TestAuthHandler, a separate
// scheme that issues no JWT, so the approved seam would have been reachable only by unit-testing the
// collaborator. That is the "a handler was called" evidence this project refuses.
//
// Every test below sends a request whose token was issued BEFORE the change and asserts the request
// FAILS. None of them asserts that the revalidator was consulted.
public class PrincipalRevalidationApiTests
{
    private const string Endpoint = "/api/members";

    private static HttpClient Client(AcmpWebApplicationFactory factory, string roles, string sub, DateTimeOffset? issuedAt = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        if (issuedAt is { } iat)
            client.DefaultRequestHeaders.Add(TestAuthHandler.IssuedAtHeader, iat.ToUnixTimeSeconds().ToString());
        return client;
    }

    [Fact]
    public async Task A_token_issued_BEFORE_a_role_change_is_refused_on_the_next_request()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedMembersAsync(("kc-target", "Target Person", CommitteeRole.Member));

        var tokenIssuedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        // The roles change AFTER that token was minted — exactly the AC-090 scenario.
        await factory.SetRevalidationStateAsync("kc-target", rolesChangedAt: DateTimeOffset.UtcNow);

        var response = await Client(factory, "Member", "kc-target", tokenIssuedAt).GetAsync(Endpoint);

        // Not 200. THIS is "the subsequent request no longer exercises the removed role": the token
        // is still signed, still unexpired, and no longer honoured.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.GetValues("X-Acmp-Auth-Reason").Should().ContainSingle().Which.Should().Be("roles_changed");
    }

    [Fact]
    public async Task A_token_issued_AFTER_the_role_change_is_accepted()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedMembersAsync(("kc-target", "Target Person", CommitteeRole.Member));
        await factory.SetRevalidationStateAsync("kc-target", rolesChangedAt: DateTimeOffset.UtcNow.AddMinutes(-10));

        var response = await Client(factory, "Member", "kc-target", DateTimeOffset.UtcNow).GetAsync(Endpoint);

        // The other half of the guard, and the one that stops a renewal loop: a token minted after
        // the change already carries the new roles, so refusing it would refuse every renewal too.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // AC-092 — proven by advancing PAST the expiry and asserting the API refuses, which is what the
    // AC demands instead of reading a countdown.
    [Fact]
    public async Task A_guest_past_their_access_window_is_refused_by_the_API()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedMembersAsync(("kc-guest", "Guest Presenter", CommitteeRole.Guest));
        await factory.SetRevalidationStateAsync("kc-guest", accessExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        var response = await Client(factory, "Guest", "kc-guest").GetAsync(Endpoint);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        // A DIFFERENT reason from roles_changed on purpose: renewing will not help an ended window,
        // and telling the SPA otherwise would drive automaticSilentRenew into a loop against a
        // session that no longer exists.
        response.Headers.GetValues("X-Acmp-Auth-Reason").Should().ContainSingle().Which.Should().Be("access_expired");
    }

    [Fact]
    public async Task A_guest_INSIDE_their_access_window_is_served()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedMembersAsync(("kc-guest", "Guest Presenter", CommitteeRole.Guest));
        await factory.SetRevalidationStateAsync("kc-guest", accessExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

        var response = await Client(factory, "Guest", "kc-guest").GetAsync(Endpoint);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_disabled_member_is_refused_even_with_a_fresh_token()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedMembersAsync(("kc-gone", "Departed Person", CommitteeRole.Member));
        await factory.SetRevalidationStateAsync("kc-gone", disable: true);

        var response = await Client(factory, "Member", "kc-gone", DateTimeOffset.UtcNow).GetAsync(Endpoint);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.GetValues("X-Acmp-Auth-Reason").Should().ContainSingle().Which.Should().Be("account_disabled");
    }

    // ⚠ THE REGRESSION THAT WOULD LOCK EVERYONE OUT. ADR-0004 provisions the member row just in time
    // on first login, so a valid token exists before the row does. If the middleware ever fails
    // closed on an unknown subject, EVERY first login breaks — including the operator's — and it
    // breaks on the path of every request. Forced here, through the pipeline, not just on the unit.
    [Fact]
    public async Task A_caller_with_NO_member_row_is_still_served_so_first_login_can_provision()
    {
        await using var factory = new AcmpWebApplicationFactory();

        var response = await Client(factory, "Member", "kc-never-provisioned", DateTimeOffset.UtcNow).GetAsync(Endpoint);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_unauthenticated_request_is_still_a_plain_401_and_carries_no_revalidation_reason()
    {
        await using var factory = new AcmpWebApplicationFactory();

        var response = await factory.CreateClient().GetAsync(Endpoint);

        // The middleware must not touch anonymous traffic: RequireAuthorization already decides it
        // (AC-008), and a revalidation reason here would misdescribe why the request was refused.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Contains("X-Acmp-Auth-Reason").Should().BeFalse();
    }
}
