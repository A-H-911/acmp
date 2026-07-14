using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Acmp.Api.Tests;

// HTTP-contract tests for /api/knowledge/templates through the real pipeline + policy authorization (docs/10).
// Reads are committee-wide; every mutation is Template.Manage. Unlike Document.Manage, Template.Manage's matrix
// row grants Administrator too (and has no allow-if-owner), so an Administrator CAN create a template here — the
// one behavioural difference from the document endpoints. The full FR-119 flow is driven end to end.
public class KnowledgeTemplateApiTests
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
        name = Loc("Topic intake", "نموذج الموضوع"),
        targetType = "Topic",
        body = "# {{title}}\n\n{{summary}}",
    };

    private sealed record TemplateSummary(Guid Id, string Key, string TargetType, string Status, int Version);
    private sealed record TemplateDetail(string Key, string TargetType, string Body, string Status, int Version);
    private sealed record Page(int Total);

    private static async Task<TemplateSummary> CreateAsync(HttpClient c)
    {
        var res = await c.PostAsJsonAsync("/api/knowledge/templates", CreateBody());
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<TemplateSummary>())!;
    }

    [Fact] // AC-008
    public async Task Create_without_token_returns_401()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, roles: null).PostAsJsonAsync("/api/knowledge/templates", CreateBody())).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // Template.Manage has no allow-if-owner and excludes Member — a Member create is 403
    public async Task Member_cannot_create_a_template_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, "Member", "kc-mem").PostAsJsonAsync("/api/knowledge/templates", CreateBody())).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // The Template.Manage difference from Document.Manage: Administrator is granted.
    public async Task Administrator_can_create_a_template()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var tpl = await CreateAsync(Client(factory, "Administrator", "kc-admin"));
        tpl.Key.Should().Be("TPL-2026-001");
        tpl.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Create_with_empty_name_returns_400()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var body = new { name = Loc("", ""), targetType = "Topic", body = "b" };
        (await Client(factory, "Secretary", "kc-sec").PostAsJsonAsync("/api/knowledge/templates", body)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact] // Secretary drives the full FR-119 flow: create → edit (bumps Version) → deprecate
    public async Task Secretary_drives_the_full_template_lifecycle()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", "kc-sec");

        var tpl = await CreateAsync(sec);
        tpl.Key.Should().Be("TPL-2026-001");
        tpl.Status.Should().Be("Active");
        tpl.Version.Should().Be(1);

        // Register + detail + filter + unknown key.
        (await (await sec.GetAsync("/api/knowledge/templates")).Content.ReadFromJsonAsync<Page>())!.Total.Should().Be(1);
        (await (await sec.GetAsync("/api/knowledge/templates?targetType=Topic")).Content.ReadFromJsonAsync<Page>())!.Total.Should().Be(1);
        (await (await sec.GetAsync("/api/knowledge/templates?targetType=Adr")).Content.ReadFromJsonAsync<Page>())!.Total.Should().Be(0);
        (await sec.GetAsync("/api/knowledge/templates/TPL-2099-999")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Edit bumps Version.
        var editBody = new { name = Loc("Topic intake v2", "نموذج ٢"), body = "# {{title}} (v2)" };
        (await sec.PutAsJsonAsync($"/api/knowledge/templates/{tpl.Id}", editBody)).StatusCode.Should().Be(HttpStatusCode.OK);

        var afterEdit = await (await sec.GetAsync($"/api/knowledge/templates/{tpl.Key}")).Content.ReadFromJsonAsync<TemplateDetail>();
        afterEdit!.Version.Should().Be(2);
        afterEdit.Body.Should().Contain("v2");
        afterEdit.TargetType.Should().Be("Topic");

        // Deprecate (soft delete, terminal).
        (await sec.PostAsync($"/api/knowledge/templates/{tpl.Id}/deprecate", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await (await sec.GetAsync($"/api/knowledge/templates/{tpl.Key}")).Content.ReadFromJsonAsync<TemplateDetail>())!.Status.Should().Be("Deprecated");
    }

    [Fact] // Editing/deprecating a Deprecated (terminal) template is a 409 Conflict
    public async Task Mutating_a_deprecated_template_returns_409()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var chair = Client(factory, "Chairman", "kc-chair");
        var tpl = await CreateAsync(chair);
        (await chair.PostAsync($"/api/knowledge/templates/{tpl.Id}/deprecate", null)).EnsureSuccessStatusCode();

        var editBody = new { name = Loc("late", "متأخر"), body = "b" };
        (await chair.PutAsJsonAsync($"/api/knowledge/templates/{tpl.Id}", editBody)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await chair.PostAsync($"/api/knowledge/templates/{tpl.Id}/deprecate", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact] // Acting on an unknown template id is a 404
    public async Task Deprecating_an_unknown_template_returns_404()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, "Chairman", "kc-chair").PostAsync($"/api/knowledge/templates/{Guid.NewGuid()}/deprecate", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
