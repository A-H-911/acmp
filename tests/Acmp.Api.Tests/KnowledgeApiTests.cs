using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Acmp.Api.Tests;

// HTTP-contract tests for /api/knowledge/documents through the real pipeline + policy authorization (docs/10).
// Reads are committee-wide; every mutation is Document.Manage. A Document is NOT topic-scoped, so — exactly like
// the ADR endpoints — the Document.Manage allow-if-owner (Member/Reviewer) has no ownership relationship to
// resolve at a bare create and Chairman/Secretary are the effective writers (a Member/Reviewer create is 403).
// The full P15d lifecycle is driven end to end.
public class KnowledgeApiTests
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
        title = Loc("Onboarding guide", "دليل الإعداد"),
        category = "Guides",
        body = Loc("# Welcome", "# مرحبا"),
        tags = new[] { "wiki", "onboarding" },
    };

    private sealed record DocumentSummary(Guid Id, string Key, string Status, int Version);
    private sealed record DocumentVersionView(int Version);
    private sealed record DocumentDetail(string Key, string Status, int Version, IReadOnlyList<DocumentVersionView> Versions);
    private sealed record Page(int Total);

    private static async Task<DocumentSummary> CreateAsync(HttpClient c)
    {
        var res = await c.PostAsJsonAsync("/api/knowledge/documents", CreateBody());
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<DocumentSummary>())!;
    }

    [Fact] // AC-008
    public async Task Create_without_token_returns_401()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, roles: null).PostAsJsonAsync("/api/knowledge/documents", CreateBody())).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // docs/10: Document.Manage denies Auditor outright
    public async Task Auditor_cannot_create_a_document_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, "Auditor").PostAsJsonAsync("/api/knowledge/documents", CreateBody())).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // Document.Manage's allow-if-owner needs an ownership relationship, which a bare create has none of
           // (a document is not topic-scoped) — so a Member/Reviewer cannot create either (mirrors AdrsApiTests).
    public async Task Member_and_reviewer_cannot_create_a_document_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, "Member", "kc-mem").PostAsJsonAsync("/api/knowledge/documents", CreateBody())).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await Client(factory, "Reviewer", "kc-rev").PostAsJsonAsync("/api/knowledge/documents", CreateBody())).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_with_empty_title_returns_400()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var body = new { title = Loc("", ""), category = "Guides", body = Loc("b", "b") };
        (await Client(factory, "Secretary", "kc-sec").PostAsJsonAsync("/api/knowledge/documents", body)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact] // Chairman drives the full P15d flow: create → edit (versions) → publish → archive
    public async Task Chairman_drives_the_full_document_lifecycle()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var chair = Client(factory, "Chairman", "kc-chair");

        var doc = await CreateAsync(chair);
        doc.Key.Should().Be("DOC-2026-001");
        doc.Status.Should().Be("Draft");
        doc.Version.Should().Be(1);

        // Register + detail + unknown key.
        (await (await chair.GetAsync("/api/knowledge/documents")).Content.ReadFromJsonAsync<Page>())!.Total.Should().Be(1);
        (await chair.GetAsync("/api/knowledge/documents/DOC-2099-999")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // FR-117: edit bumps Version + appends a snapshot.
        var editBody = new { title = Loc("Onboarding guide v2", "دليل الإعداد ٢"), category = "Playbooks", body = Loc("# Welcome (edited)", "# مرحبا") };
        (await chair.PutAsJsonAsync($"/api/knowledge/documents/{doc.Id}", editBody)).StatusCode.Should().Be(HttpStatusCode.OK);

        var afterEdit = await (await chair.GetAsync($"/api/knowledge/documents/{doc.Key}")).Content.ReadFromJsonAsync<DocumentDetail>();
        afterEdit!.Version.Should().Be(2);
        afterEdit.Versions.Should().HaveCount(2);

        // Publish, then archive (both status-only — Version stays at 2).
        (await chair.PostAsync($"/api/knowledge/documents/{doc.Id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await (await chair.GetAsync($"/api/knowledge/documents/{doc.Key}")).Content.ReadFromJsonAsync<DocumentDetail>())!.Status.Should().Be("Published");

        (await chair.PostAsync($"/api/knowledge/documents/{doc.Id}/archive", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var archived = await (await chair.GetAsync($"/api/knowledge/documents/{doc.Key}")).Content.ReadFromJsonAsync<DocumentDetail>();
        archived!.Status.Should().Be("Archived");
        archived.Version.Should().Be(2);
    }

    [Fact] // Secretary may also manage documents (Document.Manage full-allow)
    public async Task Secretary_can_create_and_publish_a_document()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "kc-sec");
        var doc = await CreateAsync(sec);

        (await sec.PostAsync($"/api/knowledge/documents/{doc.Id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await (await sec.GetAsync($"/api/knowledge/documents/{doc.Key}")).Content.ReadFromJsonAsync<DocumentDetail>())!.Status.Should().Be("Published");
    }

    [Fact] // Editing an archived (terminal) document is a 409 Conflict (domain InvalidOperationException → 409)
    public async Task Editing_an_archived_document_returns_409()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var chair = Client(factory, "Chairman", "kc-chair");
        var doc = await CreateAsync(chair);
        (await chair.PostAsync($"/api/knowledge/documents/{doc.Id}/archive", null)).EnsureSuccessStatusCode();

        var editBody = new { title = Loc("late", "متأخر"), category = "Guides", body = Loc("b", "b") };
        (await chair.PutAsJsonAsync($"/api/knowledge/documents/{doc.Id}", editBody)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        // Re-publishing an archived document is likewise a conflict.
        (await chair.PostAsync($"/api/knowledge/documents/{doc.Id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact] // Acting on an unknown document id is a 404
    public async Task Publishing_an_unknown_document_returns_404()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, "Chairman", "kc-chair").PostAsync($"/api/knowledge/documents/{Guid.NewGuid()}/publish", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
