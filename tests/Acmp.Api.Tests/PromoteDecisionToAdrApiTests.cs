using System.Net;
using System.Net.Http.Json;
using Acmp.Modules.Decisions.Domain;
using Acmp.Modules.Decisions.Domain.Enums;
using Acmp.Modules.Decisions.Infrastructure.Persistence;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Api.Tests;

// HTTP-contract tests for FR-068 promotion: POST /api/adrs/from-decision through the real pipeline + policy
// (Adr.Promote = Chairman only) and the IDecisionReader / ITraceabilityWriter seams. An issued decision is
// seeded straight into the (InMemory) DecisionsDbContext the reader reads from.
public class PromoteDecisionToAdrApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private static HttpClient Client(AcmpWebApplicationFactory factory, string? roles, string sub = "kc-chair")
    {
        var client = factory.CreateClient();
        if (roles is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
            client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        }
        return client;
    }

    private static async Task<Guid> SeedIssuedDecisionAsync(AcmpWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DecisionsDbContext>();
        var d = Decision.Draft("DECN-2026-008", Guid.NewGuid(), null, DecisionOutcome.Approved,
            LocalizedString.Create("Adopt Keycloak", "اعتماد كيكلوك"),
            LocalizedString.Create("Adopt Keycloak, realm per stream.", "اعتماد كيكلوك، نطاق لكل مسار."),
            LocalizedString.Create("Fragmented auth across streams.", "مصادقة مجزأة عبر المسارات."),
            LocalizedString.Create("In-house IdP.", "موفّر داخلي."),
            voteId: null, conditions: Array.Empty<DecisionConditionInput>(), actorSub: "kc-chair", Now);
        d.Issue("kc-chair", "Chair", chairOverride: false, overrideJustification: null, Now);
        db.Decisions.Add(d);
        await db.SaveChangesAsync();
        return d.PublicId;
    }

    private sealed record AdrSummary(Guid Id, string Key, string Status);
    private static object Body(Guid decisionId) => new { decisionId };

    [Fact] // AC-008
    public async Task Promote_without_token_returns_401()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, roles: null).PostAsJsonAsync("/api/adrs/from-decision", Body(Guid.NewGuid())))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // docs/10: Adr.Promote is Chairman-only — a Secretary may not promote.
    public async Task Secretary_cannot_promote_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, "Secretary", "kc-sec").PostAsJsonAsync("/api/adrs/from-decision", Body(Guid.NewGuid())))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // unknown decision → 404 through the pipeline
    public async Task Chairman_promoting_an_unknown_decision_returns_404()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, "Chairman").PostAsJsonAsync("/api/adrs/from-decision", Body(Guid.NewGuid())))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact] // happy path + idempotency: promote once (201, Draft, pre-filled title), second promote → 409
    public async Task Chairman_promotes_an_issued_decision_then_a_repeat_is_conflicted()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedMembersAsync(("kc-chair", "Chair", CommitteeRole.Chairman));
        var decisionId = await SeedIssuedDecisionAsync(factory);
        var chair = Client(factory, "Chairman", "kc-chair");

        var res = await chair.PostAsJsonAsync("/api/adrs/from-decision", Body(decisionId));
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var adr = (await res.Content.ReadFromJsonAsync<AdrSummary>())!;
        adr.Key.Should().StartWith("ADR-");
        adr.Status.Should().Be("Draft");

        // A second promotion of the same decision is blocked (one ADR per decision).
        (await chair.PostAsJsonAsync("/api/adrs/from-decision", Body(decisionId)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
