using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Acmp.Api.Tests;

// HTTP-contract tests for /api/research through the real pipeline + policy authorization (docs/10). Reads are
// committee-wide; every mutation is Research.Manage. A ResearchMission is NOT topic-scoped, so — exactly like
// the ADR endpoints — the Research.Manage allow-if-owner (Member/Reviewer) has no ownership relationship to
// resolve at a bare create and Chairman/Secretary are the effective writers (a Member/Reviewer create is 403).
// The full P15a lifecycle is driven end to end.
public class ResearchApiTests
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
        title = Loc("Evaluate event brokers", "تقييم وسطاء الأحداث"),
        question = Loc("Do we need a broker?", "هل نحتاج وسيطًا؟"),
        keystonePackageRef = (string?)null,
        sourceTopicId = (Guid?)null,
    };

    private sealed record MissionSummary(Guid Id, string Key, string Status);
    private sealed record FindingView(Guid Id, string Key, string Confidence, bool IsVerified);
    private sealed record RecommendationView(Guid Id, string Key, string Status);
    private sealed record MissionDetail(string Key, string Status, IReadOnlyList<FindingView> Findings, IReadOnlyList<RecommendationView> Recommendations);
    private sealed record Page(int Total);

    private static async Task<MissionSummary> CreateAsync(HttpClient c)
    {
        var res = await c.PostAsJsonAsync("/api/research", CreateBody());
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<MissionSummary>())!;
    }

    [Fact] // AC-008
    public async Task Create_without_token_returns_401()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, roles: null).PostAsJsonAsync("/api/research", CreateBody())).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // docs/10 #26: Research.Manage denies Auditor outright
    public async Task Auditor_cannot_create_a_mission_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, "Auditor").PostAsJsonAsync("/api/research", CreateBody())).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // Research.Manage's allow-if-owner needs an ownership relationship, which a bare create has none of
           // (a mission is not topic-scoped) — so a Member/Reviewer cannot create either (mirrors AdrsApiTests).
    public async Task Member_and_reviewer_cannot_create_a_mission_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, "Member", "kc-mem").PostAsJsonAsync("/api/research", CreateBody())).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await Client(factory, "Reviewer", "kc-rev").PostAsJsonAsync("/api/research", CreateBody())).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_with_empty_title_returns_400()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var body = new { title = Loc("", ""), question = Loc("q", "q") };
        (await Client(factory, "Secretary", "kc-sec").PostAsJsonAsync("/api/research", body)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact] // Chairman drives the full P15a flow: create → edit → activate → capture children → verify/decide → complete
    public async Task Chairman_drives_the_full_research_lifecycle()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var chair = Client(factory, "Chairman", "kc-chair");

        var mission = await CreateAsync(chair);
        mission.Key.Should().Be("RMS-2026-001");
        mission.Status.Should().Be("Proposed");

        // Revise the draft (still Proposed).
        (await chair.PutAsJsonAsync($"/api/research/{mission.Id}/draft", CreateBody())).StatusCode.Should().Be(HttpStatusCode.OK);

        // Register + detail + unknown key.
        (await (await chair.GetAsync("/api/research")).Content.ReadFromJsonAsync<Page>())!.Total.Should().Be(1);
        (await chair.GetAsync("/api/research/RMS-2099-999")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Activate, then capture a finding + a recommendation.
        (await chair.PostAsync($"/api/research/{mission.Id}/activate", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await chair.PostAsJsonAsync($"/api/research/{mission.Id}/findings",
            new { summary = Loc("Brokers add ops load", "الوسطاء يزيدون العبء"), detail = (object?)null, confidence = "High" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await chair.PostAsJsonAsync($"/api/research/{mission.Id}/recommendations",
            new { statement = Loc("Stay with the modular monolith", "الاستمرار بالنمط الأحادي"), rationale = (object?)null, priority = "High", linkedTopicId = (Guid?)null }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await (await chair.GetAsync($"/api/research/{mission.Key}")).Content.ReadFromJsonAsync<MissionDetail>();
        detail!.Status.Should().Be("Active");
        var finding = detail.Findings.Should().ContainSingle().Subject;
        var rec = detail.Recommendations.Should().ContainSingle().Subject;
        finding.Key.Should().Be("FND-001");
        rec.Key.Should().Be("REC-001");

        // Verify the finding + accept the recommendation.
        (await chair.PostAsync($"/api/research/{mission.Id}/findings/{finding.Id}/verify", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await chair.PostAsJsonAsync($"/api/research/{mission.Id}/recommendations/{rec.Id}/status", new { status = "Accepted" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await chair.PutAsJsonAsync($"/api/research/{mission.Id}/findings/{finding.Id}",
            new { summary = Loc("Brokers add real ops load", "عبء تشغيلي فعلي"), detail = (object?)null, confidence = "Medium" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await (await chair.GetAsync($"/api/research/{mission.Key}")).Content.ReadFromJsonAsync<MissionDetail>();
        after!.Findings.Single().IsVerified.Should().BeTrue();
        after.Findings.Single().Confidence.Should().Be("Medium");
        after.Recommendations.Single().Status.Should().Be("Accepted");

        // W16: record the accepted recommendation as converted to an execution topic (successor id).
        (await chair.PostAsJsonAsync($"/api/research/{mission.Id}/recommendations/{rec.Id}/convert", new { topicId = Guid.NewGuid() }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await (await chair.GetAsync($"/api/research/{mission.Key}")).Content.ReadFromJsonAsync<MissionDetail>())!
            .Recommendations.Single().Status.Should().Be("Converted");

        // Complete (terminal).
        (await chair.PostAsync($"/api/research/{mission.Id}/complete", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await (await chair.GetAsync($"/api/research/{mission.Key}")).Content.ReadFromJsonAsync<MissionDetail>())!.Status.Should().Be("Completed");
    }

    [Fact] // Secretary may also manage missions (Research.Manage full-allow)
    public async Task Secretary_can_create_and_cancel_a_mission()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "kc-sec");
        var mission = await CreateAsync(sec);

        (await sec.PostAsJsonAsync($"/api/research/{mission.Id}/cancel", new { reason = Loc("Out of scope", "خارج النطاق") }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await (await sec.GetAsync($"/api/research/{mission.Key}")).Content.ReadFromJsonAsync<MissionDetail>())!.Status.Should().Be("Cancelled");
    }

    [Fact] // A terminal-state re-transition is a 409 Conflict (domain InvalidOperationException → 409)
    public async Task Re_completing_a_completed_mission_returns_409()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var chair = Client(factory, "Chairman", "kc-chair");
        var mission = await CreateAsync(chair);
        (await chair.PostAsync($"/api/research/{mission.Id}/activate", null)).EnsureSuccessStatusCode();
        (await chair.PostAsync($"/api/research/{mission.Id}/complete", null)).EnsureSuccessStatusCode();

        (await chair.PostAsync($"/api/research/{mission.Id}/complete", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        // Adding a finding to a terminal mission is likewise a conflict.
        (await chair.PostAsJsonAsync($"/api/research/{mission.Id}/findings",
            new { summary = Loc("late", "متأخر"), detail = (object?)null, confidence = "Low" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact] // Acting on an unknown mission id is a 404
    public async Task Activating_an_unknown_mission_returns_404()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, "Chairman", "kc-chair").PostAsync($"/api/research/{Guid.NewGuid()}/activate", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
