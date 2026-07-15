using System.Net;
using System.Net.Http.Json;
using Acmp.Modules.Membership.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Acmp.Api.Tests;

// AC-060 (FR-143/144) — the global search endpoint through the real pipeline + policy authorization. Exercises
// the coordinator's fan-out/grouping over InMemory: every module's ISearchProvider runs its LIKE branch
// (InMemory cannot translate FREETEXT — the Arabic word-breaker leg is proven in SearchProvidersFtsTests on a
// real FTS SQL Server), a created Topic is found and returned grouped under "Topics" with its deep link.
public sealed class SearchApiTests
{
    private static HttpClient Client(WebApplicationFactory<Program> app, string? roles, string sub = "u1")
    {
        var client = app.CreateClient();
        if (roles is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
            client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        }
        return client;
    }

    private static object SubmitBody() => new
    {
        title = "Adopt Keycloak",
        description = "Consolidate IAM onto Keycloak.",
        justification = "Fragmented auth is risky.",
        type = "ArchitectureDecision",
        urgency = "Urgent",
        source = "CommitteeMember",
        streams = new[] { "identity" },
        systems = Array.Empty<string>(),
        tags = Array.Empty<string>(),
    };

    [Fact] // AC-060
    public async Task Global_search_returns_the_topic_grouped_with_a_deep_link()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedMembersAsync(("u1", "User One", CommitteeRole.Member));
        var client = Client(factory, "Member");

        (await client.PostAsJsonAsync("/api/topics", SubmitBody())).EnsureSuccessStatusCode();

        var groups = await client.GetFromJsonAsync<List<SearchGroup>>("/api/search?q=Keycloak");

        groups.Should().NotBeNull();
        var topics = groups!.Single(g => g.Type == "Topics");
        topics.Items.Should().Contain(h =>
            h.Title.En == "Adopt Keycloak" &&
            h.Key.StartsWith("TOP-") &&
            h.DeepLink == $"/topics/{h.Key}" &&
            h.Status.Length > 0);
    }

    [Fact]
    public async Task Blank_query_returns_no_groups()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedMembersAsync(("u1", "User One", CommitteeRole.Member));
        var groups = await Client(factory, "Member").GetFromJsonAsync<List<SearchGroup>>("/api/search?q=%20");
        groups.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_without_a_token_returns_401()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, roles: null).GetAsync("/api/search?q=anything");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record Loc(string En, string Ar);
    private sealed record Hit(string Type, Guid Id, string Key, Loc Title, string Excerpt, string Status, string DeepLink);
    private sealed record SearchGroup(string Type, List<Hit> Items);
}
