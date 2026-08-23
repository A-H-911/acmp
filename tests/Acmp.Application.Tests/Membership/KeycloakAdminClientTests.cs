using System.Net;
using System.Text;
using Acmp.Modules.Membership.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Acmp.Application.Tests.Membership;

// ADR-0038 — the ONLY code that calls Keycloak's Admin API. It is exercised against a stub transport
// rather than a live realm, so what is proven here is the CONTRACT: which endpoints are called, in
// what order, and what is done with the responses. The role set the service account actually needs
// is deliberately NOT asserted here — that is an empirical question about a real realm, and ADR-0038
// requires proving it on UAT rather than assuming it from documentation or from a mock.
public class KeycloakAdminClientTests
{
    private static KeycloakAdminClient Client(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        new(new HttpClient(new StubHandler(responder)) { BaseAddress = new Uri("https://kc.example/") },
            Options.Create(new KeycloakAdminOptions
            {
                Enabled = true,
                BaseUrl = "https://kc.example/",
                Realm = "acmp",
                ClientId = "acmp-admin-sa",
                ClientSecret = "s3cret",
            }));

    private static HttpResponseMessage Token() => Json("""{"access_token":"tok"}""");

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task CreateUser_returns_the_subject_from_Location_and_a_password_it_never_repeats()
    {
        var seen = new List<string>();
        var client = Client(req =>
        {
            seen.Add($"{req.Method} {req.RequestUri!.AbsolutePath}");
            if (req.RequestUri.AbsolutePath.EndsWith("/token")) return Token();
            if (req.RequestUri.AbsolutePath.EndsWith("/users"))
            {
                var created = new HttpResponseMessage(HttpStatusCode.Created);
                created.Headers.Location = new Uri("https://kc.example/admin/realms/acmp/users/kc-new-id");
                return created;
            }
            return new HttpResponseMessage(HttpStatusCode.NoContent); // reset-password
        });

        var first = await client.CreateUserAsync("new@acmp.gov", "New Person");
        var second = await client.CreateUserAsync("other@acmp.gov", "Other Person");

        first.SubjectId.Should().Be("kc-new-id", "Keycloak returns the new id in the Location header, not the body");
        first.TemporaryPassword.Should().NotBeNullOrWhiteSpace();
        // Generated per call, so nothing about it is guessable from a previous invite.
        second.TemporaryPassword.Should().NotBe(first.TemporaryPassword);
        seen.Should().Contain(s => s.Contains("/reset-password"), "the password is set temporary => must change at first login");
    }

    [Fact]
    public async Task CreateUser_surfaces_a_Keycloak_conflict_as_a_conflict()
    {
        var client = Client(req => req.RequestUri!.AbsolutePath.EndsWith("/token")
            ? Token()
            : new HttpResponseMessage(HttpStatusCode.Conflict));

        // The account exists in Keycloak but not in ACMP's roster — a real state the caller has to
        // be able to tell apart from its own duplicate check.
        await client.Invoking(c => c.CreateUserAsync("dupe@acmp.gov", "Dupe"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreateUser_fails_loudly_when_Keycloak_returns_no_Location()
    {
        var client = Client(req => req.RequestUri!.AbsolutePath.EndsWith("/token")
            ? Token()
            : new HttpResponseMessage(HttpStatusCode.Created)); // no Location header

        await client.Invoking(c => c.CreateUserAsync("x@acmp.gov", "X Y"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*Location*");
    }

    [Fact]
    public async Task SetRealmRoles_REPLACES_the_set_removing_what_was_not_requested()
    {
        var methods = new List<HttpMethod>();
        var client = Client(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.EndsWith("/token")) return Token();
            if (path.EndsWith("/available")) return Json("""[{"id":"r2","name":"Reviewer"}]""");
            if (path.EndsWith("/realm"))
            {
                methods.Add(req.Method);
                return req.Method == HttpMethod.Get
                    ? Json("""[{"id":"r1","name":"Member"}]""")
                    : new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        await client.SetRealmRolesAsync("kc-1", new[] { "Reviewer" });

        // Merging instead of replacing would make role REMOVAL impossible through this path and turn
        // every assignment into a grant-only operation.
        methods.Should().Contain(HttpMethod.Delete, "Member was held but not requested, so it is removed");
        methods.Should().Contain(HttpMethod.Post, "Reviewer was requested and available, so it is added");
    }

    [Fact]
    public async Task SetRealmRoles_makes_no_change_calls_when_the_set_already_matches()
    {
        var methods = new List<HttpMethod>();
        var client = Client(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.EndsWith("/token")) return Token();
            if (path.EndsWith("/available")) return Json("[]");
            if (path.EndsWith("/realm"))
            {
                methods.Add(req.Method);
                return req.Method == HttpMethod.Get
                    ? Json("""[{"id":"r1","name":"Member"}]""")
                    : new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        await client.SetRealmRolesAsync("kc-1", new[] { "Member" });

        methods.Should().NotContain(HttpMethod.Delete);
        methods.Should().NotContain(HttpMethod.Post);
    }

    [Fact]
    public async Task SignOutEverywhere_posts_to_the_logout_endpoint()
    {
        string? path = null;
        var client = Client(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/token")) return Token();
            path = req.RequestUri.AbsolutePath;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        await client.SignOutEverywhereAsync("kc-1");

        path.Should().EndWith("/users/kc-1/logout");
    }

    [Fact]
    public async Task DisableUser_disables_and_never_deletes()
    {
        HttpMethod? method = null;
        var client = Client(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/token")) return Token();
            method = req.Method;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        await client.DisableUserAsync("kc-1");

        // Deleting a Keycloak user strands its member row forever (DEF-029) and is what produced the
        // duplicate identities behind DEF-045. Disable is the only correct verb here.
        method.Should().Be(HttpMethod.Put);
        method.Should().NotBe(HttpMethod.Delete);
    }

    [Fact]
    public async Task A_failed_service_account_token_stops_the_operation()
    {
        var client = Client(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        await client.Invoking(c => c.SignOutEverywhereAsync("kc-1"))
            .Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task A_token_response_without_an_access_token_fails_loudly()
    {
        var client = Client(_ => Json("""{"not_a_token":"x"}"""));

        await client.Invoking(c => c.DisableUserAsync("kc-1"))
            .Should().ThrowAsync<Exception>();
    }

    // ---- ListUsersAsync (SC-011) — the port's one READ ----

    [Fact]
    public async Task ListUsers_returns_each_account_with_the_realm_roles_it_holds()
    {
        var client = Client(req => req.RequestUri!.AbsolutePath switch
        {
            var p when p.EndsWith("/token") => Token(),
            var p when p.EndsWith("/role-mappings/realm") => Json(
                """[{"id":"r1","name":"default-roles-acmp"},{"id":"r2","name":"Reviewer"}]"""),
            _ => Json(
                """[{"id":"kc-1","username":"a@acmp.gov","email":"a@acmp.gov","firstName":"Aisha","lastName":"Noor","enabled":true}]"""),
        });

        var accounts = await client.ListUsersAsync();

        var account = accounts.Should().ContainSingle().Subject;
        account.SubjectId.Should().Be("kc-1");
        account.Email.Should().Be("a@acmp.gov");
        account.FullName.Should().Be("Aisha Noor", "the adapter rejoins what CreateUserAsync's SplitName took apart");
        account.Enabled.Should().BeTrue();
        // Raw and unmapped, Keycloak's own composite included: deciding which of these is a committee
        // role is the application's job, and a port that pre-filtered would hide an unrecognised one.
        account.RealmRoles.Should().BeEquivalentTo(new[] { "default-roles-acmp", "Reviewer" });
    }

    // ⚠ THE ONE WITH TEETH. Keycloak's own default is max=100 and it applies SILENTLY, so a single
    // call is a listing that becomes wrong at 101 accounts while still reporting success — and a
    // reconciliation that cannot see an account is exactly the failure it exists to fix (DEF-065).
    [Fact]
    public async Task ListUsers_keeps_paging_while_a_page_comes_back_full()
    {
        var pagesRequested = new List<string>();
        var client = Client(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/token")) return Token();
            if (req.RequestUri.AbsolutePath.EndsWith("/role-mappings/realm")) return Json("[]");

            pagesRequested.Add(req.RequestUri.Query);
            // A FULL first page (100), then a short one — the only signal that there is more.
            var full = req.RequestUri.Query.Contains("first=0");
            var count = full ? 100 : 3;
            var users = Enumerable.Range(0, count).Select(i =>
                $$"""{"id":"kc-{{(full ? i : 100 + i)}}","username":"u{{i}}","email":"","firstName":"","lastName":"","enabled":true}""");
            return Json("[" + string.Join(",", users) + "]");
        });

        var accounts = await client.ListUsersAsync();

        accounts.Should().HaveCount(103, "a full page means there may be more, and stopping there would lose the rest");
        pagesRequested.Should().HaveCount(2);
        pagesRequested[1].Should().Contain("first=100", "the second page must start where the first ended");
    }

    [Fact]
    public async Task ListUsers_falls_back_to_the_username_when_an_account_carries_no_name()
    {
        var client = Client(req => req.RequestUri!.AbsolutePath switch
        {
            var p when p.EndsWith("/token") => Token(),
            var p when p.EndsWith("/role-mappings/realm") => Json("[]"),
            _ => Json("""[{"id":"kc-2","username":"seeded@acmp.gov","email":"seeded@acmp.gov","enabled":false}]"""),
        });

        var account = (await client.ListUsersAsync()).Should().ContainSingle().Subject;

        // A member row with an EMPTY display name is a roster line nobody can identify.
        account.FullName.Should().Be("seeded@acmp.gov");
        account.Enabled.Should().BeFalse("a disabled account is carried, not filtered — the caller reports the skip");
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(_responder(request));
    }
}
