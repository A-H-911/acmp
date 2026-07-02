using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Acmp.Api.Tests;

// HTTP-contract tests for /api/risks through the real pipeline + policy authorization (docs/10). Reads are
// committee-wide; raise/mitigate/close/escalate are Risk.Manage; accept is the narrower Risk.Accept
// (Chairman/Secretary only, no allow-if-owner). The acting subject/roles are set per request via the test
// auth header, so the authorization narrowing is exercised end to end.
public class RisksApiTests
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

    private static object RaiseBody(string owner = "kc-owner") => new
    {
        title = Loc("Auth migration risk", "خطر الترحيل"),
        description = (object?)null,
        likelihood = "Medium",
        impact = "High",
        ownerUserId = owner,
        ownerName = "Owner",
        subjectType = "Topic",
        subjectId = Guid.NewGuid(),
        subjectKey = "TOP-2026-014",
        initialMitigation = (object?)null,
    };

    private sealed record RiskSummary(Guid Id, string Key, string Status, string Exposure);
    private sealed record RiskDetail(string Key, string Status, string? AcceptingAuthority);
    private sealed record Page(int Total);

    private static async Task<RiskSummary> RaiseAsync(HttpClient c, string owner = "kc-owner")
    {
        var res = await c.PostAsJsonAsync("/api/risks", RaiseBody(owner));
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<RiskSummary>())!;
    }

    [Fact] // AC-008
    public async Task Raise_without_token_returns_401()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, roles: null).PostAsJsonAsync("/api/risks", RaiseBody())).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // docs/10 row 16: Risk.Manage denies Auditor
    public async Task Auditor_cannot_raise_a_risk_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, "Auditor").PostAsJsonAsync("/api/risks", RaiseBody())).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Raise_with_empty_title_returns_400()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var body = new { title = Loc("", ""), likelihood = "Medium", impact = "High", ownerUserId = "kc-owner", ownerName = "Owner", subjectType = "Topic", subjectId = Guid.NewGuid() };
        (await Client(factory, "Secretary", "kc-sec").PostAsJsonAsync("/api/risks", body)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact] // W15: raise → detail → register; unknown key 404; exposure projected
    public async Task Secretary_raises_then_reads_detail_and_register()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "kc-sec");

        var risk = await RaiseAsync(sec);
        risk.Key.Should().Be("RSK-2026-001");
        risk.Status.Should().Be("Open");
        risk.Exposure.Should().Be("High"); // Medium × High

        (await sec.GetAsync($"/api/risks/{risk.Key}")).StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await (await sec.GetAsync("/api/risks")).Content.ReadFromJsonAsync<Page>();
        page!.Total.Should().BeGreaterThanOrEqualTo(1);
        (await sec.GetAsync("/api/risks/RSK-2026-999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact] // W15 mitigation → begin → close lifecycle
    public async Task Full_mitigation_lifecycle_closes_the_risk()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "kc-sec");
        var risk = await RaiseAsync(sec);

        (await sec.PostAsJsonAsync($"/api/risks/{risk.Id}/mitigations",
            new { description = Loc("Dual-run", "تشغيل مزدوج"), type = "Reduce", ownerUserId = (string?)null, linkedActionId = (Guid?)null, dueDate = (DateTimeOffset?)null }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await sec.PostAsync($"/api/risks/{risk.Id}/begin-mitigation", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await sec.PostAsJsonAsync($"/api/risks/{risk.Id}/close", new { closureNote = Loc("handled", "تمت المعالجة") }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await (await sec.GetAsync($"/api/risks/{risk.Key}")).Content.ReadFromJsonAsync<RiskDetail>();
        detail!.Status.Should().Be("Closed");
    }

    [Fact] // Risk.Accept: Secretary allowed (204); Member denied (403)
    public async Task Accept_is_allowed_for_secretary_and_denied_for_member()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "kc-sec");
        var risk = await RaiseAsync(sec);

        (await Client(factory, "Member", "kc-mem").PostAsJsonAsync($"/api/risks/{risk.Id}/accept",
            new { rationale = Loc("accepted", "مقبول"), authority = "Chairman" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await sec.PostAsJsonAsync($"/api/risks/{risk.Id}/accept",
            new { rationale = Loc("accepted", "مقبول"), authority = "Chairman" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await (await sec.GetAsync($"/api/risks/{risk.Key}")).Content.ReadFromJsonAsync<RiskDetail>();
        detail!.Status.Should().Be("Accepted");
        detail.AcceptingAuthority.Should().Be("Chairman");
    }

    [Fact] // escalate transitions to Escalated (empty roster → no recipients, still 204)
    public async Task Escalate_moves_the_risk_to_escalated()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "kc-sec");
        var risk = await RaiseAsync(sec);

        (await sec.PostAsJsonAsync($"/api/risks/{risk.Id}/escalate", new { reason = Loc("severe", "خطير"), target = "Steering Board" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await (await sec.GetAsync($"/api/risks/{risk.Key}")).Content.ReadFromJsonAsync<RiskDetail>();
        detail!.Status.Should().Be("Escalated");
    }
}
