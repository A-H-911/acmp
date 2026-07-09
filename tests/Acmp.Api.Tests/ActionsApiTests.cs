using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Acmp.Api.Tests;

// HTTP-contract tests for /api/actions through the real pipeline + policy authorization (docs/10).
// Reads are committee-wide; create + the lifecycle transitions are Action.Create; verify is Action.Verify
// with the SoD-1 guard (verifier ≠ owner/completer) inside the handler (AC-012/013). The acting subject is
// set per request via the test auth header, so owner-vs-verifier identity is exercised end to end.
public class ActionsApiTests
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

    private static object CreateBody(string owner = "kc-owner") => new
    {
        title = Loc("Draft the ADR", "صياغة السجل"),
        description = (object?)null,
        priority = "Normal",
        ownerUserId = owner,
        ownerName = "Owner",
        dueDate = (DateTimeOffset?)null,
        sourceType = "Decision",
        sourceId = Guid.NewGuid(),
        sourceKey = "DECN-2026-008",
        meetingKey = "MTG-2026-018",
    };

    private sealed record ActionSummary(Guid Id, string Key, string Status, string OwnerUserId);
    private sealed record ActionDetail(string Key, string Status, string? VerifiedByUserId);
    private sealed record Page(int Total);

    private static async Task<ActionSummary> CreateAsync(HttpClient c, string owner = "kc-owner")
    {
        var res = await c.PostAsJsonAsync("/api/actions", CreateBody(owner));
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<ActionSummary>())!;
    }

    [Fact] // AC-008
    public async Task Create_without_token_returns_401()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, roles: null).PostAsJsonAsync("/api/actions", CreateBody());
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // docs/10: Action.Create denies Reviewer
    public async Task Reviewer_cannot_create_an_action_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, "Reviewer").PostAsJsonAsync("/api/actions", CreateBody());
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_with_empty_title_returns_400()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var body = new { title = Loc("", ""), priority = "Normal", ownerUserId = "kc-owner", ownerName = "Owner", sourceType = "Decision", sourceId = Guid.NewGuid() };
        var response = await Client(factory, "Secretary", "kc-sec").PostAsJsonAsync("/api/actions", body);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact] // W13: create → detail → register; unknown key 404
    public async Task Secretary_creates_then_reads_detail_and_register()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "kc-sec");

        var action = await CreateAsync(sec);
        action.Key.Should().Be("ACT-2026-001");
        action.Status.Should().Be("Open");

        var detail = await sec.GetAsync($"/api/actions/{action.Key}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        (await detail.Content.ReadFromJsonAsync<ActionDetail>())!.Status.Should().Be("Open");

        var page = await (await sec.GetAsync("/api/actions")).Content.ReadFromJsonAsync<Page>();
        page!.Total.Should().BeGreaterThanOrEqualTo(1);

        (await sec.GetAsync("/api/actions/ACT-2026-999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact] // AC-013: full W14 flow — an independent verifier closes it (Completed → Verified)
    public async Task Independent_verifier_completes_the_lifecycle()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "kc-sec");
        var action = await CreateAsync(sec, owner: "kc-owner");

        (await sec.PostAsync($"/api/actions/{action.Id}/start", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await sec.PostAsJsonAsync($"/api/actions/{action.Id}/complete", new { completionNote = (object?)null })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // kc-third is neither the owner (kc-owner) nor the completer (kc-sec) → SoD-1 allows it.
        var verifier = Client(factory, "Secretary", "kc-third");
        (await verifier.PostAsync($"/api/actions/{action.Id}/verify", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await (await sec.GetAsync($"/api/actions/{action.Key}")).Content.ReadFromJsonAsync<ActionDetail>();
        detail!.Status.Should().Be("Verified");
        detail.VerifiedByUserId.Should().Be("kc-third");
    }

    [Fact] // AC-012: the owner cannot verify their own action → 403, stays Completed
    public async Task Owner_cannot_verify_their_own_action_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "kc-sec");
        var action = await CreateAsync(sec, owner: "kc-owner");
        await sec.PostAsync($"/api/actions/{action.Id}/start", null);
        await sec.PostAsJsonAsync($"/api/actions/{action.Id}/complete", new { completionNote = (object?)null });

        var owner = Client(factory, "Secretary", "kc-owner");
        (await owner.PostAsync($"/api/actions/{action.Id}/verify", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var detail = await (await sec.GetAsync($"/api/actions/{action.Key}")).Content.ReadFromJsonAsync<ActionDetail>();
        detail!.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task Verify_unknown_action_returns_404()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var res = await Client(factory, "Secretary", "kc-sec").PostAsync($"/api/actions/{Guid.NewGuid()}/verify", null);
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Endpoint-body coverage: the block/unblock/progress/cancel handlers are thin pass-throughs to their
    // (already unit-tested) commands; this walks a valid transition path so each HTTP body returns 204.
    [Fact]
    public async Task Progress_block_unblock_cancel_transitions_return_204()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "kc-sec");
        var action = await CreateAsync(sec);

        (await sec.PostAsJsonAsync($"/api/actions/{action.Id}/progress", new { progressPct = 25 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent); // Open allows progress
        (await sec.PostAsync($"/api/actions/{action.Id}/start", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await sec.PostAsJsonAsync($"/api/actions/{action.Id}/block", new { reason = Loc("Waiting on vendor", "بانتظار المورد") }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await sec.PostAsync($"/api/actions/{action.Id}/unblock", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await sec.PostAsJsonAsync($"/api/actions/{action.Id}/cancel", new { reason = Loc("No longer needed", "لم يعد مطلوبًا") }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await (await sec.GetAsync($"/api/actions/{action.Key}")).Content.ReadFromJsonAsync<ActionDetail>();
        detail!.Status.Should().Be("Cancelled");
    }
}
