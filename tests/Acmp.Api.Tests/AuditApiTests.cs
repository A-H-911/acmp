using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Acmp.Api.Tests;

// HTTP-contract tests for /api/audit through the real pipeline + policy authorization (AC-017/019/020,
// ADR-0027). Read-only: the register + on-demand chain-verify. RBAC = {Auditor, Chairman, Secretary} → 200;
// Member/Administrator → 403 (Administrator excluded on SoD-5, ADR-0027 supersedes the FR-153 role clause);
// no token → 401 (AC-008). The store is seeded with BOTH a lean v1 row and an enriched v2 row so the DTO's
// cross-shape normalization (Action ?? EventType; Actor ?? Subject) is exercised, not just the v2 path.
public class AuditApiTests
{
    private static HttpClient Client(AcmpWebApplicationFactory factory, string? roles, string sub = "kc-aud")
    {
        var client = factory.CreateClient();
        if (roles is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
            client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        }
        return client;
    }

    private sealed record AuditRow(
        long Sequence, DateTimeOffset OccurredAt, int HashVersion, string Action,
        string? SubjectType, string? SubjectId, string? Actor, string? ActorRole, string? Outcome,
        string? BeforeJson, string? AfterJson, string? CorrelationId);

    private sealed record AuditPage(IReadOnlyList<AuditRow> Items, int Total, int Page, int PageSize);

    private sealed record VerifyResult(bool IsValid, long? BrokenAtSequence, string? Reason);

    [Fact] // AC-008
    public async Task Register_without_token_returns_401()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, roles: null).GetAsync("/api/audit"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory] // AC-020: only Auditor/Chairman/Secretary may read the record
    [InlineData("Member")]
    [InlineData("Reviewer")]
    [InlineData("Administrator")] // ADR-0027: SoD-5 — the sysadmin manages the system but not the record
    public async Task Non_audit_role_cannot_read_audit_403(string role)
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, role).GetAsync("/api/audit"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory] // AC-020: the allowed readers
    [InlineData("Auditor")]
    [InlineData("Chairman")]
    [InlineData("Secretary")]
    public async Task Audit_role_reads_register_200(string role)
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedAuditAsync();
        var res = await Client(factory, role).GetAsync("/api/audit");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadFromJsonAsync<AuditPage>())!.Total.Should().Be(2);
    }

    [Fact] // AC-017: the register surfaces both row shapes, normalized + newest-first
    public async Task Register_returns_both_v1_and_v2_rows_normalized()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedAuditAsync();

        var page = await (await Client(factory, "Auditor").GetAsync("/api/audit"))
            .Content.ReadFromJsonAsync<AuditPage>();

        // Newest-first: the v2 row (Sequence 2) is first.
        page!.Items.Should().HaveCount(2);
        var v2 = page.Items[0];
        v2.HashVersion.Should().Be(2);
        v2.Action.Should().Be("Vote.Closed");
        v2.SubjectType.Should().Be("Vote");
        v2.SubjectId.Should().Be("VOTE-2026-001");
        v2.Actor.Should().Be("kc-chair");
        v2.Outcome.Should().Be("Success");
        v2.AfterJson.Should().Be("{\"status\":\"Closed\"}");

        // The lean v1 row normalizes: Action <- EventType, Actor <- Subject; enriched fields null.
        var v1 = page.Items[1];
        v1.HashVersion.Should().Be(1);
        v1.Action.Should().Be("Authentication.NoRoleClaim");
        v1.Actor.Should().Be("kc-legacy");
        v1.SubjectType.Should().BeNull();
        v1.Outcome.Should().BeNull();
    }

    [Fact] // AC-017: entityType filters the CLR aggregate name (v2 rows only)
    public async Task Filter_by_entityType_returns_only_matching()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedAuditAsync();

        var page = await (await Client(factory, "Auditor").GetAsync("/api/audit?entityType=Vote"))
            .Content.ReadFromJsonAsync<AuditPage>();

        page!.Total.Should().Be(1);
        page.Items.Should().ContainSingle().Which.SubjectType.Should().Be("Vote");
    }

    [Fact] // AC-017: actor filter matches across both row shapes (COALESCE on ActorUserId/Subject)
    public async Task Filter_by_actor_matches_v1_subject()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedAuditAsync();

        var page = await (await Client(factory, "Auditor").GetAsync("/api/audit?actor=kc-legacy"))
            .Content.ReadFromJsonAsync<AuditPage>();

        page!.Total.Should().Be(1);
        page.Items.Should().ContainSingle().Which.Action.Should().Be("Authentication.NoRoleClaim");
    }

    [Fact] // AC-017: paginated
    public async Task Page_size_one_returns_first_page_of_two()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedAuditAsync();

        var page = await (await Client(factory, "Auditor").GetAsync("/api/audit?page=1&pageSize=1"))
            .Content.ReadFromJsonAsync<AuditPage>();

        page!.Total.Should().Be(2);
        page.Items.Should().ContainSingle();
        page.PageSize.Should().Be(1);
    }

    [Fact] // AC-017: action filter (COALESCE across shapes) + date-range bounds
    public async Task Filter_by_action_and_date_range()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedAuditAsync();
        var aud = Client(factory, "Auditor");

        // action matches the v2 row's Action verb.
        var byAction = await (await aud.GetAsync("/api/audit?action=Vote.Closed"))
            .Content.ReadFromJsonAsync<AuditPage>();
        byAction!.Total.Should().Be(1);
        byAction.Items.Should().ContainSingle().Which.SubjectType.Should().Be("Vote");

        // A future-only window excludes both seeded rows; an all-time window includes both.
        var future = DateTimeOffset.UtcNow.AddDays(1).ToString("O");
        var empty = await (await aud.GetAsync($"/api/audit?from={Uri.EscapeDataString(future)}"))
            .Content.ReadFromJsonAsync<AuditPage>();
        empty!.Total.Should().Be(0);

        var past = DateTimeOffset.UtcNow.AddDays(-1).ToString("O");
        var all = await (await aud.GetAsync($"/api/audit?from={Uri.EscapeDataString(past)}&to={Uri.EscapeDataString(future)}"))
            .Content.ReadFromJsonAsync<AuditPage>();
        all!.Total.Should().Be(2);
    }

    [Fact] // AC-019: on-demand chain verify over an intact chain
    public async Task Verify_returns_valid_for_intact_chain()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedAuditAsync();

        var res = await Client(factory, "Auditor").GetAsync("/api/audit/verify");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var verify = await res.Content.ReadFromJsonAsync<VerifyResult>();
        verify!.IsValid.Should().BeTrue();
        verify.BrokenAtSequence.Should().BeNull();
    }

    [Fact] // AC-019/020: verify is also gated — a non-audit role is refused
    public async Task Verify_denied_to_non_audit_role_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        (await Client(factory, "Administrator").GetAsync("/api/audit/verify"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
