using System.Net;
using System.Net.Http.Json;
using Acmp.Modules.Membership.Domain.Enums;
using FluentAssertions;

namespace Acmp.Api.Tests;

// HTTP-contract tests for /api/adrs through the real pipeline + policy authorization (docs/10). Reads are
// committee-wide; create/edit/propose/request-changes are Adr.Create; approve is Adr.Approve; deprecate/
// supersede are Adr.Supersede. The full W17/W21 lifecycle is driven end to end, exercising the notification
// fan-out against the seeded committee roster.
public class AdrsApiTests : IClassFixture<AcmpWebApplicationFactory>
{
    // WBS-27.2 / DEC-124 d1 - ONE host per class instead of one per test method. Every method
    // below now shares this host's fourteen InMemory databases, so a test that asserts over a
    // global count sees what its siblings wrote. SharedHostOrderGuard is the control for that.
    private readonly AcmpWebApplicationFactory _factory;

    public AdrsApiTests(AcmpWebApplicationFactory factory) => _factory = factory.Reset();

    private static HttpClient Client(AcmpWebApplicationFactory factory, string? roles, string sub = "u1")
    {
        var client = factory.CreateClient();
        if (roles is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
            client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        }
        return client;
    }

    private static object Loc(string en, string ar) => new { en, ar };

    private static object CreateBody() => new
    {
        title = Loc("Adopt Keycloak", "اعتماد Keycloak"),
        context = Loc("We need OIDC", "نحتاج OIDC"),
        decisionDrivers = (object?)null,
        decisionText = Loc("Use Keycloak", "استخدام Keycloak"),
        consequencesPositive = (object?)null,
        consequencesNegative = (object?)null,
        options = new[] { new { name = Loc("Keycloak", "Keycloak"), body = (object?)null, isChosen = true } },
    };

    private static object SupersedeBody() => new
    {
        title = Loc("Adopt Zitadel", "اعتماد Zitadel"),
        context = Loc("Keycloak retired", "أُوقف Keycloak"),
        decisionDrivers = (object?)null,
        decisionText = Loc("Use Zitadel", "استخدام Zitadel"),
        consequencesPositive = (object?)null,
        consequencesNegative = (object?)null,
        options = (object?)null,
        reason = Loc("Newer IdP", "هوية أحدث"),
    };

    private sealed record AdrSummary(Guid Id, string Key, string Status);
    private sealed record AdrDetail(string Key, string Status, string? SupersededByAdrKey);
    private sealed record Page(int Total);

    private static async Task<AdrSummary> CreateAsync(HttpClient c)
    {
        var res = await c.PostAsJsonAsync("/api/adrs", CreateBody());
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<AdrSummary>())!;
    }

    [Fact] // AC-008
    public async Task Create_without_token_returns_401()
    {
        var factory = _factory;
        (await Client(factory, roles: null).PostAsJsonAsync("/api/adrs", CreateBody())).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // docs/10: Adr.Create denies Auditor
    public async Task Auditor_cannot_create_an_adr_403()
    {
        var factory = _factory;
        (await Client(factory, "Auditor").PostAsJsonAsync("/api/adrs", CreateBody())).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_with_empty_title_returns_400()
    {
        var factory = _factory;
        var body = new { title = Loc("", ""), context = Loc("c", "c"), decisionText = Loc("d", "d") };
        (await Client(factory, "Secretary", "kc-sec").PostAsJsonAsync("/api/adrs", body)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact] // docs/10: Adr.Approve is Chairman/Secretary only — a Reviewer may not approve. (Standalone ADR
           // authoring is likewise Chairman/Secretary: Adr.Create's allow-if-owner needs an ownership
           // relationship, which a bare create has none of, so a Reviewer cannot create either.)
    public async Task Reviewer_cannot_approve_403()
    {
        var factory = _factory;
        var sec = Client(factory, "Secretary", "kc-sec");
        var created = await CreateAsync(sec);
        (await sec.PostAsync($"/api/adrs/{created.Id}/propose", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var rev = Client(factory, "Reviewer", "kc-rev");
        (await rev.PostAsync($"/api/adrs/{created.Id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // W17/W21: create → edit → propose → approve → supersede; reads along the way
    public async Task Secretary_drives_the_full_adr_lifecycle()
    {
        var factory = _factory;
        await factory.SeedMembersAsync(("kc-sec", "Sam", CommitteeRole.Secretary), ("kc-m1", "M1", CommitteeRole.Member));
        var sec = Client(factory, "Secretary", "kc-sec");

        var created = await CreateAsync(sec);

        // Revise the draft.
        (await sec.PutAsJsonAsync($"/api/adrs/{created.Id}/draft", CreateBody())).StatusCode.Should().Be(HttpStatusCode.OK);

        // Register + detail.
        (await (await sec.GetAsync("/api/adrs")).Content.ReadFromJsonAsync<Page>())!.Total.Should().Be(1);
        var detail = await (await sec.GetAsync($"/api/adrs/{created.Key}")).Content.ReadFromJsonAsync<AdrDetail>();
        detail!.Status.Should().Be("Draft");
        (await sec.GetAsync("/api/adrs/ADR-2099-999")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Propose → request-changes → propose → approve.
        (await sec.PostAsync($"/api/adrs/{created.Id}/propose", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await sec.PostAsync($"/api/adrs/{created.Id}/request-changes", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await sec.PostAsync($"/api/adrs/{created.Id}/propose", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await sec.PostAsync($"/api/adrs/{created.Id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Supersede the approved ADR → a new approved successor; the prior links to it.
        var supRes = await sec.PostAsJsonAsync($"/api/adrs/{created.Id}/supersede", SupersedeBody());
        supRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var successor = (await supRes.Content.ReadFromJsonAsync<AdrSummary>())!;
        successor.Status.Should().Be("Approved");

        var prior = await (await sec.GetAsync($"/api/adrs/{created.Key}")).Content.ReadFromJsonAsync<AdrDetail>();
        prior!.Status.Should().Be("Superseded");
        prior.SupersededByAdrKey.Should().Be(successor.Key);
    }

    [Fact] // W21: deprecate an approved ADR (Adr.Supersede)
    public async Task Secretary_deprecates_an_approved_adr()
    {
        var factory = _factory;
        await factory.SeedMembersAsync(("kc-sec", "Sam", CommitteeRole.Secretary));
        var sec = Client(factory, "Secretary", "kc-sec");

        var created = await CreateAsync(sec);
        (await sec.PostAsync($"/api/adrs/{created.Id}/propose", null)).EnsureSuccessStatusCode();
        (await sec.PostAsync($"/api/adrs/{created.Id}/approve", null)).EnsureSuccessStatusCode();

        var res = await sec.PostAsJsonAsync($"/api/adrs/{created.Id}/deprecate", new { reason = Loc("Obsolete", "متقادم") });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await (await sec.GetAsync($"/api/adrs/{created.Key}")).Content.ReadFromJsonAsync<AdrDetail>();
        detail!.Status.Should().Be("Deprecated");
    }
}
