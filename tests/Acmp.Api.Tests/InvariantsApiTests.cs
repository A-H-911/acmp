using System.Net;
using System.Net.Http.Json;
using Acmp.Modules.Membership.Domain.Enums;
using FluentAssertions;

namespace Acmp.Api.Tests;

// HTTP-contract tests for /api/invariants through the real pipeline + policy authorization (docs/10 rows
// 21/22). Reads are committee-wide; create/edit/propose/request-changes are Invariant.Create; approve is
// Invariant.Approve; retire/supersede are Invariant.Approve. The full W18/W21 lifecycle is driven end to end,
// exercising the notification fan-out against the seeded committee roster.
public class InvariantsApiTests
{
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
        category = "Security",
        scope = "Platform",
        statement = Loc("No cross-module DB access", "لا وصول لقاعدة وحدة أخرى"),
        rationale = Loc("Preserves boundaries", "يحافظ على الحدود"),
        exceptionsPolicy = (object?)null,
        ownerUserId = "kc-sec",
        ownerName = "Sam",
    };

    private static object SupersedeBody() => new
    {
        category = "Data",
        scope = "OrgWide",
        statement = Loc("All data stays on-prem", "تبقى البيانات محلياً"),
        rationale = Loc("Residency law", "قانون الإقامة"),
        exceptionsPolicy = (object?)null,
        ownerUserId = "kc-sec",
        ownerName = "Sam",
        reason = Loc("Stronger rule", "قاعدة أقوى"),
    };

    private sealed record InvariantSummary(Guid Id, string Key, string Status);
    private sealed record InvariantDetail(string Key, string Status, string? SupersededByInvariantKey);
    private sealed record Page(int Total);

    private static async Task<InvariantSummary> CreateAsync(HttpClient c)
    {
        var res = await c.PostAsJsonAsync("/api/invariants", CreateBody());
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<InvariantSummary>())!;
    }

    [Fact] // AC-008
    public async Task Create_without_token_returns_401()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, roles: null).PostAsJsonAsync("/api/invariants", CreateBody())).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // docs/10 row 21: Invariant.Create denies Auditor
    public async Task Auditor_cannot_create_an_invariant_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, "Auditor").PostAsJsonAsync("/api/invariants", CreateBody())).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_with_empty_statement_returns_400()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var body = new { category = "Security", scope = "Platform", statement = Loc("", ""), rationale = Loc("r", "r"), ownerUserId = "kc-sec", ownerName = "Sam" };
        (await Client(factory, "Secretary", "kc-sec").PostAsJsonAsync("/api/invariants", body)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact] // docs/10 row 22: Invariant.Approve is Chairman/Secretary only — a Reviewer may not approve.
    public async Task Reviewer_cannot_approve_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "kc-sec");
        var created = await CreateAsync(sec);
        (await sec.PostAsync($"/api/invariants/{created.Id}/propose", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var rev = Client(factory, "Reviewer", "kc-rev");
        (await rev.PostAsync($"/api/invariants/{created.Id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // W18/W21: create → edit → propose → approve → supersede; reads along the way
    public async Task Secretary_drives_the_full_invariant_lifecycle()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedMembersAsync(("kc-sec", "Sam", CommitteeRole.Secretary), ("kc-m1", "M1", CommitteeRole.Member));
        var sec = Client(factory, "Secretary", "kc-sec");

        var created = await CreateAsync(sec);

        // Revise the draft.
        (await sec.PutAsJsonAsync($"/api/invariants/{created.Id}/draft", CreateBody())).StatusCode.Should().Be(HttpStatusCode.OK);

        // Register + detail.
        (await (await sec.GetAsync("/api/invariants")).Content.ReadFromJsonAsync<Page>())!.Total.Should().Be(1);
        var detail = await (await sec.GetAsync($"/api/invariants/{created.Key}")).Content.ReadFromJsonAsync<InvariantDetail>();
        detail!.Status.Should().Be("Draft");
        (await sec.GetAsync("/api/invariants/AIV-2099-999")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Propose → request-changes → propose → approve.
        (await sec.PostAsync($"/api/invariants/{created.Id}/propose", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await sec.PostAsync($"/api/invariants/{created.Id}/request-changes", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await sec.PostAsync($"/api/invariants/{created.Id}/propose", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await sec.PostAsync($"/api/invariants/{created.Id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Supersede the active invariant → a new active successor; the prior links to it.
        var supRes = await sec.PostAsJsonAsync($"/api/invariants/{created.Id}/supersede", SupersedeBody());
        supRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var successor = (await supRes.Content.ReadFromJsonAsync<InvariantSummary>())!;
        successor.Status.Should().Be("Active");

        var prior = await (await sec.GetAsync($"/api/invariants/{created.Key}")).Content.ReadFromJsonAsync<InvariantDetail>();
        prior!.Status.Should().Be("Superseded");
        prior.SupersededByInvariantKey.Should().Be(successor.Key);
    }

    [Fact] // W21: retire an active invariant (Invariant.Approve)
    public async Task Secretary_retires_an_active_invariant()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedMembersAsync(("kc-sec", "Sam", CommitteeRole.Secretary));
        var sec = Client(factory, "Secretary", "kc-sec");

        var created = await CreateAsync(sec);
        (await sec.PostAsync($"/api/invariants/{created.Id}/propose", null)).EnsureSuccessStatusCode();
        (await sec.PostAsync($"/api/invariants/{created.Id}/approve", null)).EnsureSuccessStatusCode();

        var res = await sec.PostAsJsonAsync($"/api/invariants/{created.Id}/retire", new { reason = Loc("Obsolete", "متقادم") });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await (await sec.GetAsync($"/api/invariants/{created.Key}")).Content.ReadFromJsonAsync<InvariantDetail>();
        detail!.Status.Should().Be("Retired");
    }
}
