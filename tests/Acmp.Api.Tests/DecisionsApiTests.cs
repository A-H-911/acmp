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

    // AC-029: satisfy the downstream-link gate by creating a follow-up action sourced from the decision
    // (the real cross-module seam — /api/actions writes the (SourceType=Decision, SourceId) reference the
    // IActionLinkDirectory then counts). Chairman is allowed to create actions (docs/10).
    private static async Task LinkActionTo(HttpClient client, Guid decisionId)
    {
        var body = new
        {
            title = Loc("Follow-up action", "إجراء متابعة"),
            description = (object?)null,
            priority = "Normal",
            ownerUserId = "kc-owner",
            ownerName = "Owner",
            dueDate = (DateTimeOffset?)null,
            sourceType = "Decision",
            sourceId = decisionId,
            sourceKey = (string?)null,
            meetingKey = (string?)null,
        };
        (await client.PostAsJsonAsync("/api/actions", body)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

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

    [Fact] // W12: a Chairman issues a recorded decision (Draft → Issued) — AC-029 satisfied by a linked action
    public async Task Chairman_issues_a_recorded_decision()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var chair = Client(factory, "Chairman", "kc-chair");
        var decision = await (await chair.PostAsJsonAsync("/api/decisions", RecordBody())).Content.ReadFromJsonAsync<DecisionSummary>();
        await LinkActionTo(chair, decision!.Id);   // Approved is follow-up-bearing → needs ≥1 downstream link

        var issue = await chair.PostAsJsonAsync($"/api/decisions/{decision.Id}/issue", new { chairOverride = false, overrideJustification = (object?)null });
        issue.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await (await chair.GetAsync($"/api/decisions/{decision.Key}")).Content.ReadFromJsonAsync<DecisionDetail>();
        detail!.Status.Should().Be("Issued");
    }

    [Fact] // AC-029 (OQ-045 failing-first): a follow-up-bearing decision with NO downstream link is rejected
    public async Task Issue_followup_decision_without_a_downstream_link_returns_409_and_stays_draft()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var chair = Client(factory, "Chairman", "kc-chair");
        var decision = await (await chair.PostAsJsonAsync("/api/decisions", RecordBody())).Content.ReadFromJsonAsync<DecisionSummary>();

        var rejected = await chair.PostAsJsonAsync($"/api/decisions/{decision!.Id}/issue", new { chairOverride = false, overrideJustification = (object?)null });
        rejected.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var stillDraft = await (await chair.GetAsync($"/api/decisions/{decision.Key}")).Content.ReadFromJsonAsync<DecisionDetail>();
        stillDraft!.Status.Should().Be("Draft");

        // once a follow-up action links to it, the same issue succeeds (real cross-module seam)
        await LinkActionTo(chair, decision.Id);
        var accepted = await chair.PostAsJsonAsync($"/api/decisions/{decision.Id}/issue", new { chairOverride = false, overrideJustification = (object?)null });
        accepted.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact] // AC-029 exemption: a non-follow-up outcome (Rejected) issues with no downstream link required
    public async Task Issue_non_followup_decision_without_a_link_succeeds()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var chair = Client(factory, "Chairman", "kc-chair");
        var body = new
        {
            topicId = Guid.NewGuid(),
            meetingId = (Guid?)null,
            outcome = "Rejected",
            title = Loc("Reject the proposal", "رفض المقترح"),
            rationale = Loc("Out of scope", "خارج النطاق"),
            alternatives = (object?)null,
            voteId = (Guid?)null,
            conditions = Array.Empty<object>(),
        };
        var decision = await (await chair.PostAsJsonAsync("/api/decisions", body)).Content.ReadFromJsonAsync<DecisionSummary>();

        var issue = await chair.PostAsJsonAsync($"/api/decisions/{decision!.Id}/issue", new { chairOverride = false, overrideJustification = (object?)null });
        issue.StatusCode.Should().Be(HttpStatusCode.NoContent);
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
        await LinkActionTo(chair, prior!.Id);   // AC-029: the prior (Approved) needs a link before it can be issued
        await chair.PostAsJsonAsync($"/api/decisions/{prior.Id}/issue", new { chairOverride = false, overrideJustification = (object?)null });

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
