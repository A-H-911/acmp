using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Acmp.Api.Tests;

// HTTP-contract tests for /api/votes through the real pipeline + policy authorization (docs/10). Reads by key
// (detail) or topic Guid (list); mutations by the vote's Guid id. Configure/open/close are Vote.Manage
// (Chairman/Secretary); cast/change/recuse are Vote.Cast (Chairman/Member). Votes with no linked meeting skip
// the present-quorum check, so the flow needs no seeded attendance.
public class VotesApiTests
{
    private static HttpClient Client(AcmpWebApplicationFactory factory, string? roles, string sub)
    {
        var client = factory.CreateClient();
        if (roles is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
            client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        }
        return client;
    }

    private static object ConfigBody(Guid? topicId = null, int minCast = 1) => new
    {
        topicId = topicId ?? Guid.NewGuid(),
        meetingId = (Guid?)null,
        options = new[] { "Approve", "Reject" },
        allowAbstain = true,
        minPresent = 0,
        minCast,
        eligibleVoters = new[]
        {
            new { userId = "u-alice", name = "Alice" },
            new { userId = "u-bob", name = "Bob" },
        },
    };

    private sealed record VoteSummary(Guid Id, string Key, string Status);
    private sealed record BallotInfo(string VoterUserId, string? Choice, bool Recused);
    private sealed record VoteDetail(string Key, string Status, List<BallotInfo> Ballots);

    [Fact] // AC-008
    public async Task Configure_without_token_returns_401()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await factory.CreateClient().PostAsJsonAsync("/api/votes", ConfigBody());
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // docs/10: Vote.Manage is Chairman/Secretary — a Member is forbidden
    public async Task Member_cannot_configure_a_vote_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, "Member", "u-mem").PostAsJsonAsync("/api/votes", ConfigBody());
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Configure_with_one_option_returns_400()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var body = new
        {
            topicId = Guid.NewGuid(),
            options = new[] { "OnlyOne" },
            allowAbstain = false,
            minPresent = 0,
            minCast = 1,
            eligibleVoters = new[] { new { userId = "u-alice", name = "Alice" } },
        };
        var response = await Client(factory, "Secretary", "u-sec").PostAsJsonAsync("/api/votes", body);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact] // W11 full round-trip: configure → open → cast → close, then read detail + list
    public async Task Full_vote_flow_configure_open_cast_close()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "u-sec");
        var topic = Guid.NewGuid();

        var configure = await sec.PostAsJsonAsync("/api/votes", ConfigBody(topic, minCast: 1));
        configure.StatusCode.Should().Be(HttpStatusCode.Created);
        var vote = await configure.Content.ReadFromJsonAsync<VoteSummary>();
        vote!.Key.Should().Be("VOTE-2026-001");
        vote.Status.Should().Be("Configured");

        (await sec.PostAsJsonAsync($"/api/votes/{vote.Id}/open", new { })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var alice = Client(factory, "Member", "u-alice");
        (await alice.PostAsJsonAsync($"/api/votes/{vote.Id}/cast", new { choice = "Approve", comment = (object?)null }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await sec.PostAsJsonAsync($"/api/votes/{vote.Id}/close", new { })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await (await sec.GetAsync($"/api/votes/{vote.Key}")).Content.ReadFromJsonAsync<VoteDetail>();
        detail!.Status.Should().Be("Closed");
        detail.Ballots.Single(b => b.VoterUserId == "u-alice").Choice.Should().Be("Approve");

        var list = await (await sec.GetAsync($"/api/votes?topic={topic}")).Content.ReadFromJsonAsync<List<VoteSummary>>();
        list!.Should().ContainSingle().Which.Key.Should().Be("VOTE-2026-001");

        (await sec.GetAsync("/api/votes/VOTE-2026-999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact] // docs/10: cast is Vote.Cast (Chairman/Member) — a Secretary is forbidden
    public async Task Secretary_cannot_cast_a_ballot_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "u-sec");
        var vote = await (await sec.PostAsJsonAsync("/api/votes", ConfigBody())).Content.ReadFromJsonAsync<VoteSummary>();
        await sec.PostAsJsonAsync($"/api/votes/{vote!.Id}/open", new { });

        var cast = await sec.PostAsJsonAsync($"/api/votes/{vote.Id}/cast", new { choice = "Approve", comment = (object?)null });
        cast.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // AC-024: close below the cast quorum → 409 and the vote stays Open
    public async Task Close_without_quorum_returns_409_and_stays_open()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "u-sec");
        var vote = await (await sec.PostAsJsonAsync("/api/votes", ConfigBody(minCast: 2))).Content.ReadFromJsonAsync<VoteSummary>();
        await sec.PostAsJsonAsync($"/api/votes/{vote!.Id}/open", new { });
        await Client(factory, "Member", "u-alice").PostAsJsonAsync($"/api/votes/{vote.Id}/cast", new { choice = "Approve", comment = (object?)null });

        var close = await sec.PostAsJsonAsync($"/api/votes/{vote.Id}/close", new { });
        close.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var detail = await (await sec.GetAsync($"/api/votes/{vote.Key}")).Content.ReadFromJsonAsync<VoteDetail>();
        detail!.Status.Should().Be("Open");
    }

    [Fact] // AC-025: a ballot cannot be cast after the vote is closed (immutable) → 409
    public async Task Cast_after_close_returns_409()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "u-sec");
        var vote = await (await sec.PostAsJsonAsync("/api/votes", ConfigBody(minCast: 1))).Content.ReadFromJsonAsync<VoteSummary>();
        await sec.PostAsJsonAsync($"/api/votes/{vote!.Id}/open", new { });
        var alice = Client(factory, "Member", "u-alice");
        await alice.PostAsJsonAsync($"/api/votes/{vote.Id}/cast", new { choice = "Approve", comment = (object?)null });
        await sec.PostAsJsonAsync($"/api/votes/{vote.Id}/close", new { });

        var lateCast = await Client(factory, "Member", "u-bob")
            .PostAsJsonAsync($"/api/votes/{vote.Id}/cast", new { choice = "Reject", comment = (object?)null });
        lateCast.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Cast_second_ballot_returns_409()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "u-sec");
        var vote = await (await sec.PostAsJsonAsync("/api/votes", ConfigBody(minCast: 1))).Content.ReadFromJsonAsync<VoteSummary>();
        await sec.PostAsJsonAsync($"/api/votes/{vote!.Id}/open", new { });
        var alice = Client(factory, "Member", "u-alice");
        await alice.PostAsJsonAsync($"/api/votes/{vote.Id}/cast", new { choice = "Approve", comment = (object?)null });

        var again = await alice.PostAsJsonAsync($"/api/votes/{vote.Id}/cast", new { choice = "Reject", comment = (object?)null });
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Open_unknown_vote_returns_404()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var open = await Client(factory, "Secretary", "u-sec").PostAsJsonAsync($"/api/votes/{Guid.NewGuid()}/open", new { });
        open.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact] // W11: while the vote is Open a voter recasts (change) and another recuses — both 204
    public async Task Change_ballot_and_recuse_while_open_return_204()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "u-sec");
        var vote = await (await sec.PostAsJsonAsync("/api/votes", ConfigBody(minCast: 1))).Content.ReadFromJsonAsync<VoteSummary>();
        await sec.PostAsJsonAsync($"/api/votes/{vote!.Id}/open", new { });

        var alice = Client(factory, "Member", "u-alice");
        await alice.PostAsJsonAsync($"/api/votes/{vote.Id}/cast", new { choice = "Approve", comment = (object?)null });
        (await alice.PostAsJsonAsync($"/api/votes/{vote.Id}/change", new { choice = "Reject", comment = (object?)null }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var bob = Client(factory, "Member", "u-bob");
        (await bob.PostAsJsonAsync($"/api/votes/{vote.Id}/recuse", new { }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await (await sec.GetAsync($"/api/votes/{vote.Key}")).Content.ReadFromJsonAsync<VoteDetail>();
        detail!.Ballots.Single(b => b.VoterUserId == "u-alice").Choice.Should().Be("Reject");
        detail.Ballots.Single(b => b.VoterUserId == "u-bob").Recused.Should().BeTrue();
    }
}
