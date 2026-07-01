using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Acmp.Api.Tests;

// HTTP-contract tests for /api/decisions through the real pipeline + policy authorization (docs/10).
// Reads are by key (detail) or by topic Guid (list); mutations are by the decision's Guid id. Record is
// Secretary/Chairman; issue/supersede are Chairman-only.
public class DecisionsApiTests
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

    private static object RecordBody(Guid? topicId = null) => new
    {
        topicId = topicId ?? Guid.NewGuid(),
        meetingId = (Guid?)null,
        outcome = "Approved",
        title = Loc("Adopt Keycloak", "اعتماد كيكلوك"),
        rationale = Loc("Sound choice", "اختيار سليم"),
        alternatives = (object?)null,
        voteId = (Guid?)null,
        conditions = Array.Empty<object>(),
    };

    private sealed record DecisionSummary(Guid Id, string Key, Guid TopicId, string Status);
    private sealed record ConditionInfo(string Status);
    private sealed record DecisionDetail(string Key, string Status, Guid? SupersededByDecisionId, List<ConditionInfo> Conditions);

    [Fact] // AC-008
    public async Task Record_without_token_returns_401()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, roles: null).PostAsJsonAsync("/api/decisions", RecordBody());
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // docs/10: Decision.Record is Secretary/Chairman — a Member is forbidden
    public async Task Member_cannot_record_a_decision_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, "Member").PostAsJsonAsync("/api/decisions", RecordBody());
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Record_with_empty_rationale_returns_400()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var body = new
        {
            topicId = Guid.NewGuid(),
            outcome = "Approved",
            rationale = Loc("", ""),
            conditions = Array.Empty<object>(),
        };
        var response = await Client(factory, "Secretary", "kc-sec").PostAsJsonAsync("/api/decisions", body);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact] // W12: record → detail → list-by-topic; unknown key 404
    public async Task Secretary_records_then_reads_detail_and_list()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "kc-sec");
        var topic = Guid.NewGuid();

        var record = await sec.PostAsJsonAsync("/api/decisions", RecordBody(topic));
        record.StatusCode.Should().Be(HttpStatusCode.Created);
        var decision = await record.Content.ReadFromJsonAsync<DecisionSummary>();
        decision!.Key.Should().Be("DECN-2026-001");
        decision.Status.Should().Be("Draft");

        var detail = await sec.GetAsync($"/api/decisions/{decision.Key}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        (await detail.Content.ReadFromJsonAsync<DecisionDetail>())!.Status.Should().Be("Draft");

        var list = await (await sec.GetAsync($"/api/decisions?topic={topic}")).Content.ReadFromJsonAsync<List<DecisionSummary>>();
        list!.Should().ContainSingle().Which.Key.Should().Be("DECN-2026-001");

        var missing = await sec.GetAsync("/api/decisions/DECN-2026-999");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact] // W12: a Chairman issues a recorded decision (Draft → Issued)
    public async Task Chairman_issues_a_recorded_decision()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var chair = Client(factory, "Chairman", "kc-chair");
        var decision = await (await chair.PostAsJsonAsync("/api/decisions", RecordBody())).Content.ReadFromJsonAsync<DecisionSummary>();

        var issue = await chair.PostAsJsonAsync($"/api/decisions/{decision!.Id}/issue", new { chairOverride = false, overrideJustification = (object?)null });
        issue.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await (await chair.GetAsync($"/api/decisions/{decision.Key}")).Content.ReadFromJsonAsync<DecisionDetail>();
        detail!.Status.Should().Be("Issued");
    }

    [Fact] // docs/10: issue is Chairman-only — a Secretary is forbidden
    public async Task Secretary_cannot_issue_a_decision_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var decision = await (await Client(factory, "Secretary", "kc-sec")
            .PostAsJsonAsync("/api/decisions", RecordBody())).Content.ReadFromJsonAsync<DecisionSummary>();

        var issue = await Client(factory, "Secretary", "kc-sec")
            .PostAsJsonAsync($"/api/decisions/{decision!.Id}/issue", new { chairOverride = false, overrideJustification = (object?)null });
        issue.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // W21: supersede an issued decision — successor 201 Issued, prior flips to Superseded with a back-link
    public async Task Chairman_supersedes_an_issued_decision()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var chair = Client(factory, "Chairman", "kc-chair");
        var prior = await (await chair.PostAsJsonAsync("/api/decisions", RecordBody())).Content.ReadFromJsonAsync<DecisionSummary>();
        await chair.PostAsJsonAsync($"/api/decisions/{prior!.Id}/issue", new { chairOverride = false, overrideJustification = (object?)null });

        var supersede = await chair.PostAsJsonAsync($"/api/decisions/{prior.Id}/supersede", new
        {
            outcome = "Approved",
            title = Loc("Adopt Keycloak (v2)", "اعتماد كيكلوك (٢)"),
            rationale = Loc("Corrected", "مصحح"),
            alternatives = (object?)null,
            conditions = Array.Empty<object>(),
            reason = Loc("Scope changed", "تغير النطاق"),
        });
        supersede.StatusCode.Should().Be(HttpStatusCode.Created);
        var successor = await supersede.Content.ReadFromJsonAsync<DecisionSummary>();
        successor!.Key.Should().Be("DECN-2026-002");
        successor.Status.Should().Be("Issued");

        var priorDetail = await (await chair.GetAsync($"/api/decisions/{prior.Key}")).Content.ReadFromJsonAsync<DecisionDetail>();
        priorDetail!.Status.Should().Be("Superseded");
        priorDetail.SupersededByDecisionId.Should().Be(successor.Id);
    }

    [Fact]
    public async Task Issue_unknown_decision_returns_404()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var issue = await Client(factory, "Chairman", "kc-chair")
            .PostAsJsonAsync($"/api/decisions/{Guid.NewGuid()}/issue", new { chairOverride = false, overrideJustification = (object?)null });
        issue.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
