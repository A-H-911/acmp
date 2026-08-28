using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Acmp.Api.Tests;

/*
 * WBS-24.7 (DW-063 / NFR-010; DEC-084 d3) — the stream taxonomy becomes configuration.
 *
 * NFR-010 has two clauses. "No hard-coded stream limit" already held and was verified. "Stream count
 * is configuration-driven" did NOT: the five streams were seeded by raw SQL inside a migration and
 * Stream.Create had no caller, so a sixth stream meant a code change and a deployment. These two
 * endpoints are the missing caller.
 *
 * ⚠ THE REFUSALS ARE THE FEATURE HERE AS MUCH AS THE WRITES ARE, so each is proven by FORCING it.
 * The stream taxonomy is the ABAC scope vocabulary — topics carry stream codes and the intersect
 * resolves on them — so a role that could add or rename one could reshape what other members may
 * write. Hiding the control in the SPA is presentation gating; the policy is what enforces it.
 *
 * ⚠ AND WHAT NO TEST HERE CAN SEE: the UNIQUE index on Code. These run on EF InMemory, which does not
 * enforce it (DEF-066). The database-level refusal lives in Acmp.Integration.Tests, the only real SQL
 * Server — asserting it here would pass vacuously.
 */
public class AdminStreamEndpointsTests
{
    private sealed record CreatedStream(Guid PublicId);
    private sealed record StreamRef(Guid PublicId, string Code, string NameEn, string NameAr, bool IsWildcard);

    private static HttpClient Client(AcmpWebApplicationFactory factory, string roles, string sub = "kc-admin")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        return client;
    }

    private static object NewStream(string code) => new { Code = code, NameEn = "Mobile", NameAr = "الجوال" };

    [Fact] // The clause NFR-010 turns on: a stream is added without a migration, and it reads back.
    public async Task Administrator_adds_a_stream_and_it_appears_in_the_taxonomy()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var client = Client(factory, "Administrator");

        var created = await client.PostAsJsonAsync("/api/members/streams", NewStream("mobile"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await created.Content.ReadFromJsonAsync<CreatedStream>();
        body!.PublicId.Should().NotBeEmpty();

        // Read through the endpoint the SPA actually uses, so this asserts the round trip rather than
        // the handler's return value.
        var listed = await client.GetFromJsonAsync<List<StreamRef>>("/api/members/streams");
        listed!.Should().ContainSingle(s => s.PublicId == body.PublicId)
            .Which.Code.Should().Be("mobile");
    }

    [Fact] // ADR-0043's bypass surface must not widen by a single row through this path.
    public async Task A_stream_added_at_runtime_is_never_the_wildcard()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var client = Client(factory, "Administrator");

        await client.PostAsJsonAsync("/api/members/streams", NewStream("data"));

        var listed = await client.GetFromJsonAsync<List<StreamRef>>("/api/members/streams");
        listed!.Single(s => s.Code == "data").IsWildcard.Should().BeFalse();
    }

    [Fact] // FORCED REFUSAL. Secretary is the closest non-Administrator with real committee authority.
    public async Task A_secretary_cannot_add_a_stream()
    {
        await using var factory = new AcmpWebApplicationFactory();

        var response = await Client(factory, "Secretary", sub: "kc-sec")
            .PostAsJsonAsync("/api/members/streams", NewStream("shadow"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // FORCED REFUSAL on the rename half — the same scope vocabulary, the same policy.
    public async Task An_auditor_cannot_rename_a_stream()
    {
        await using var factory = new AcmpWebApplicationFactory();

        var response = await Client(factory, "Auditor", sub: "kc-aud")
            .PutAsJsonAsync($"/api/members/streams/{Guid.NewGuid()}", new { NameEn = "X", NameAr = "س" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // SEC-178's "edit inline": the display text changes and the scope key does not.
    public async Task Administrator_renames_a_stream_without_moving_its_code()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var client = Client(factory, "Administrator");

        var created = await client.PostAsJsonAsync("/api/members/streams", NewStream("platform"));
        var publicId = (await created.Content.ReadFromJsonAsync<CreatedStream>())!.PublicId;

        var renamed = await client.PutAsJsonAsync($"/api/members/streams/{publicId}",
            new { NameEn = "Platform & Infrastructure", NameAr = "المنصّة والبنية" });
        renamed.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listed = await client.GetFromJsonAsync<List<StreamRef>>("/api/members/streams");
        var stream = listed!.Single(s => s.PublicId == publicId);
        stream.NameEn.Should().Be("Platform & Infrastructure");
        stream.NameAr.Should().Be("المنصّة والبنية");
        // ⚠ THE LOAD-BEARING HALF. Topics carry the code and the ABAC intersect resolves on it, so a
        // rename that moved it would silently re-scope every topic naming the old value.
        stream.Code.Should().Be("platform");
    }

    [Fact] // A validator refusal must arrive as a 400, not as a 500 or a silent success.
    public async Task An_unusable_scope_key_is_refused_before_anything_is_written()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var client = Client(factory, "Administrator");

        var response = await client.PostAsJsonAsync("/api/members/streams", NewStream("has space"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var listed = await client.GetFromJsonAsync<List<StreamRef>>("/api/members/streams");
        listed!.Should().NotContain(s => s.Code.Contains(' '));
    }
}
