using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Acmp.Api.Tests;

/*
 * WBS-24.5 (DW-036 / FR-155, NFR-059, NFR-060; DEC-080 / SC-035) — retention configuration.
 *
 * ⚠ WHAT THESE TESTS DELIBERATELY DO NOT ASSERT: that any retention period is set. SEC-080 says periods
 * are "configurable but unset in v1" and OQ-DATA-004 leaves the values to legal, so the criterion is
 * about the MECHANISM. A test that asserted a shipped value would be asserting a decision nobody has made.
 *
 * ⚠ AND WHAT NO TEST HERE CAN SEE: the UNIQUE index on Key. These run on EF InMemory, which does not
 * enforce it (DEF-066 — a write that had never run against SQL Server under four green suites). The
 * upsert-vs-duplicate behaviour needs Acmp.Integration.Tests, which is the only real SQL Server.
 */
public class AdminRetentionEndpointsTests
{
    private sealed record RetentionPolicy(bool AutomaticPurgeEnabled, List<RetentionSetting> Settings);
    private sealed record RetentionSetting(string Key, string ValueJson);

    private static HttpClient Client(AcmpWebApplicationFactory factory, string roles, string sub = "kc-admin")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        return client;
    }

    [Fact] // NFR-059/060's v1 posture is reported as a FACT, and v1 genuinely ships no periods.
    public async Task Retention_reads_empty_with_automatic_purge_reported_off()
    {
        await using var factory = new AcmpWebApplicationFactory();

        var response = await Client(factory, "Administrator").GetAsync("/api/admin/retention");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<RetentionPolicy>();
        body.Should().NotBeNull();
        // The clause NFR-059 and NFR-060 both turn on. It is a constant rather than a setting precisely
        // so nothing can flip it on for a purge that does not exist (SEC-089 puts enforcement in Phase 2).
        body!.AutomaticPurgeEnabled.Should().BeFalse();
        body.Settings.Should().BeEmpty();
    }

    [Fact] // SEC-077: a retention config change is PRIVILEGED. Prove the refusal by forcing it.
    public async Task Non_administrator_cannot_read_retention()
    {
        await using var factory = new AcmpWebApplicationFactory();

        var response = await Client(factory, "Secretary", sub: "kc-sec").GetAsync("/api/admin/retention");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // The write is the privileged half — a reader-adjacent role must not reach it either.
    public async Task Non_administrator_cannot_write_retention()
    {
        await using var factory = new AcmpWebApplicationFactory();

        var response = await Client(factory, "Auditor", sub: "kc-aud")
            .PutAsJsonAsync("/api/admin/retention/retention.topic.years", new { ValueJson = "{\"years\":7}" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // The mechanism: an Administrator sets a period and it reads back. No value ships; one is SET.
    public async Task Administrator_sets_a_retention_period_and_reads_it_back()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var client = Client(factory, "Administrator");

        var put = await client.PutAsJsonAsync(
            "/api/admin/retention/retention.topic.years", new { ValueJson = "{\"years\":7}" });
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var body = await (await client.GetAsync("/api/admin/retention")).Content
            .ReadFromJsonAsync<RetentionPolicy>();
        body!.Settings.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new RetentionSetting("retention.topic.years", "{\"years\":7}"));
        // Setting a period must NOT switch anything on: v1 purges nothing regardless of what is configured.
        body.AutomaticPurgeEnabled.Should().BeFalse();
    }

    [Fact] // The key is identity, so a second write REPLACES rather than duplicating.
    public async Task Writing_the_same_key_twice_replaces_the_value()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var client = Client(factory, "Administrator");

        await client.PutAsJsonAsync("/api/admin/retention/retention.topic.years", new { ValueJson = "{\"years\":7}" });
        await client.PutAsJsonAsync("/api/admin/retention/retention.topic.years", new { ValueJson = "{\"years\":10}" });

        var body = await (await client.GetAsync("/api/admin/retention")).Content
            .ReadFromJsonAsync<RetentionPolicy>();
        body!.Settings.Should().ContainSingle().Which.ValueJson.Should().Be("{\"years\":10}");
    }

    [Fact] // Casing and whitespace are normalized, or one setting silently becomes two rows.
    public async Task Key_casing_and_whitespace_do_not_create_a_second_setting()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var client = Client(factory, "Administrator");

        await client.PutAsJsonAsync("/api/admin/retention/retention.topic.years", new { ValueJson = "{\"years\":7}" });
        await client.PutAsJsonAsync("/api/admin/retention/ Retention.Topic.Years ", new { ValueJson = "{\"years\":9}" });

        var body = await (await client.GetAsync("/api/admin/retention")).Content
            .ReadFromJsonAsync<RetentionPolicy>();
        body!.Settings.Should().ContainSingle().Which.ValueJson.Should().Be("{\"years\":9}");
    }

    [Fact] // The Configuration table is SHARED, so this endpoint owns the `retention.` namespace and no more.
    public async Task A_key_outside_the_retention_namespace_is_refused()
    {
        await using var factory = new AcmpWebApplicationFactory();

        var response = await Client(factory, "Administrator")
            .PutAsJsonAsync("/api/admin/retention/smtp.password", new { ValueJson = "{\"v\":1}" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact] // Value is stored AS JSON (SEC-103), so malformed input is refused at the boundary, not later.
    public async Task A_value_that_is_not_json_is_refused()
    {
        await using var factory = new AcmpWebApplicationFactory();

        var response = await Client(factory, "Administrator")
            .PutAsJsonAsync("/api/admin/retention/retention.topic.years", new { ValueJson = "seven years" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
