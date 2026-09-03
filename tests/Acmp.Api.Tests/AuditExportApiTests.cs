using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Audit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Api.Tests;

// HTTP-contract tests for GET /api/audit/export — WBS-24.6 (DW-035 / FR-154), AC-152.
//
// ⚠ THE ROLE SET UNDER TEST IS NOT FR-154's. The requirement's own text says "accessible only to Auditor
// and Administrator"; ADR-0027 supersedes that clause and decides {Auditor, Chairman, Secretary} with
// Administrator EXCLUDED on SoD-5 grounds, naming exporting explicitly (DEC-081 d2 / SC-036 reconciled the
// register to it). The Administrator row in the 403 theory is therefore the POINT of that theory, not an
// incidental case: the refusal is the feature, and it is proven by forcing it.
[Trait("Category", "Security")]
public class AuditExportApiTests : IClassFixture<AcmpWebApplicationFactory>
{
    // WBS-27.2 / DEC-124 d1 - ONE host per class instead of one per test method. Every method
    // below now shares this host's fourteen InMemory databases, so a test that asserts over a
    // global count sees what its siblings wrote. SharedHostOrderGuard is the control for that.
    private readonly AcmpWebApplicationFactory _factory;

    public AuditExportApiTests(AcmpWebApplicationFactory factory) => _factory = factory.Reset();

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

    private sealed record ExportRow(
        long Sequence, DateTimeOffset OccurredAt, int HashVersion, string Action,
        string? SubjectType, string? SubjectId, string? Actor, string? ActorRole, string? Outcome,
        string? BeforeJson, string? AfterJson, string? CorrelationId);

    // Seeds `count` chained rows of one action, plus (optionally) one row carrying Arabic content.
    // ⚠ Local rather than added to the factory's shared SeedAuditAsync: WBS-24.3 learned that widening
    // shared test data is a change to every test that reads it, and AuditApiTests asserts Total == 2.
    private static async Task<int> SeedChainAsync(
        AcmpWebApplicationFactory factory, int count, string action = "Topic.Edited", bool arabic = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var t = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var prev = AuditEvent.Genesis;
        var batch = new List<AuditEvent>(count + 1);

        for (var i = 0; i < count; i++)
        {
            var e = AuditEvent.CreateEnriched(prev, t.AddSeconds(i), action, "Topic", $"TOP-{i:D4}",
                "kc-secretary", "Secretary", AuditOutcome.Success, null, "{\"title\":\"x\"}", $"trace-{i}");
            prev = e.Hash;
            batch.Add(e);
        }

        if (arabic)
        {
            // Arabic in BOTH an actor-role position and inside the after-JSON blob: the blob is the field
            // that also carries commas and quotes, so it exercises the BOM and the CSV escaping together.
            var e = AuditEvent.CreateEnriched(prev, t.AddSeconds(count), "Topic.Edited", "Topic", "TOP-AR",
                "kc-arabic", "الأمين", AuditOutcome.Success, null,
                "{\"title\":\"لجنة العمارة\",\"note\":\"قرار, نهائي\"}", "trace-ar");
            batch.Add(e);
        }

        db.AuditEvents.AddRange(batch);
        await db.SaveChangesAsync();
        return batch.Count;
    }

    private static async Task<string> CsvAsync(HttpResponseMessage res)
    {
        // Read as BYTES, never as a string: HttpContent.ReadAsStringAsync strips the BOM, so a
        // string-based assertion for it passes whether or not the server sent one. That is a hollow
        // pass of exactly the shape WBS-24.4's dual-form finding was about.
        var bytes = await res.Content.ReadAsByteArrayAsync();
        return Encoding.UTF8.GetString(bytes);
    }

    [Fact] // AC-008
    public async Task Export_without_token_returns_401()
    {
        var factory = _factory;
        (await Client(factory, roles: null).GetAsync("/api/audit/export"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory] // AC-152 / ADR-0027 — the refusal is the feature; Administrator is the row that matters.
    [InlineData("Administrator")]
    [InlineData("Member")]
    [InlineData("Reviewer")]
    [InlineData("Submitter")]
    public async Task Non_audit_role_cannot_export_403(string role)
    {
        var factory = _factory;
        (await Client(factory, role).GetAsync("/api/audit/export"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory] // AC-152 — ADR-0027's set, and only it
    [InlineData("Auditor")]
    [InlineData("Chairman")]
    [InlineData("Secretary")]
    public async Task Audit_role_can_export_200(string role)
    {
        var factory = _factory;
        await SeedChainAsync(factory, 3);
        var res = await Client(factory, role).GetAsync("/api/audit/export");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
    }

    [Theory] // Boundary validation: an unknown format is a 400, never a silent fallback to CSV.
    [InlineData("xlsx")]
    [InlineData("pdf")]
    [InlineData("")]
    public async Task Unknown_format_is_rejected_400(string format)
    {
        var factory = _factory;
        (await Client(factory, "Auditor").GetAsync($"/api/audit/export?format={format}"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact] // FR-154 names CSV *or* JSON; both must actually be reachable.
    public async Task Json_format_returns_the_same_rows_as_json()
    {
        var factory = _factory;
        await SeedChainAsync(factory, 3);

        var res = await Client(factory, "Auditor").GetAsync("/api/audit/export?format=json");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var rows = JsonSerializer.Deserialize<List<ExportRow>>(
            await res.Content.ReadAsStringAsync(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        rows.Should().HaveCount(3);
        rows.Select(r => r.Sequence).Should().BeInAscendingOrder("an export is read as a record, in chain order");
        rows[0].SubjectId.Should().Be("TOP-0000");
        rows[0].Actor.Should().Be("kc-secretary");
    }

    [Fact] // The BOM (Excel/Arabic) + RFC-4180 escaping, asserted on bytes.
    public async Task Csv_carries_a_utf8_bom_and_survives_arabic_and_embedded_commas()
    {
        var factory = _factory;
        await SeedChainAsync(factory, 1, arabic: true);

        var res = await Client(factory, "Auditor").GetAsync("/api/audit/export?format=csv");
        var bytes = await res.Content.ReadAsByteArrayAsync();

        // The BOM, byte for byte. Mutating the '﻿' away in the endpoint fails HERE and nowhere else.
        // (Excel reads a BOM-less UTF-8 CSV in the system codepage, which mojibakes every Arabic field.)
        bytes.Take(3).Should().Equal(new byte[] { 0xEF, 0xBB, 0xBF });

        var csv = Encoding.UTF8.GetString(bytes);
        csv.Should().Contain("sequence,occurredAt,hashVersion,action");
        csv.Should().Contain("لجنة العمارة", "Arabic content must survive the round-trip intact");
        csv.Should().Contain("الأمين");
        // The after-JSON blob contains a comma AND quotes; correct escaping doubles the quotes and keeps
        // the whole blob inside ONE field, so the row still has 12 columns.
        csv.Should().Contain("\"\"note\"\":\"\"قرار, نهائي\"\"");
    }

    [Fact] // C-AUDIT-08: "every report/data export is an audited sensitive event (who, scope, volume)".
    public async Task Export_writes_an_audit_event_naming_who_scope_and_volume()
    {
        var factory = _factory;
        // 37, not a single digit: a one-character assertion would match a stray character anywhere in the
        // payload and pass whether or not the volume was recorded — LL-015's shape in an assertion.
        var seeded = await SeedChainAsync(factory, 37);

        var res = await Client(factory, "Auditor", sub: "kc-the-auditor")
            .GetAsync("/api/audit/export?format=json&entityType=Topic");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var row = await db.AuditEvents.AsNoTracking()
            .Where(e => e.EventType == "audit.exported")
            .OrderByDescending(e => e.Sequence)
            .FirstOrDefaultAsync();

        row.Should().NotBeNull("the export itself is a sensitive event and must be in the record");
        row!.Subject.Should().Be("kc-the-auditor", "WHO");
        var data = row.DataJson ?? string.Empty;
        data.Should().Contain("Topic", "SCOPE — the filters as received");
        data.Should().Contain("json");
        // VOLUME, asserted with the property NAME attached so the number cannot be matched incidentally.
        data.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Should().ContainEquivalentOf($"\"rowcount\":{seeded.ToString(CultureInfo.InvariantCulture)}");
    }

    // ⛔ THE ANTI-REGRESSION TEST FOR THIS ITEM. DEF-104 capped every paged read at PageSize.Clamp's 500,
    // and applying that habit here would silently truncate the compliance artifact — DEF-103's shape on
    // the worst possible surface, indistinguishable from "those rows do not exist". This test fails the
    // moment anyone routes the export back through the register's paging.
    [Fact]
    public async Task Export_is_not_truncated_by_the_paged_read_cap()
    {
        var factory = _factory;
        const int Beyond = 640; // > PageSize.Clamp's Max of 500
        await SeedChainAsync(factory, Beyond);

        var rows = JsonSerializer.Deserialize<List<ExportRow>>(
            await (await Client(factory, "Auditor").GetAsync("/api/audit/export?format=json"))
                .Content.ReadAsStringAsync(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        rows.Should().HaveCount(Beyond, "an export that stops at the page cap is a corrupted audit record");
        rows[^1].SubjectId.Should().Be($"TOP-{Beyond - 1:D4}", "the LAST row is the one truncation would eat");
    }

    [Fact] // The register and the export must select the same set — one predicate, two callers.
    public async Task Export_and_register_agree_on_the_same_filters()
    {
        var factory = _factory;
        await SeedChainAsync(factory, 5, action: "Topic.Edited");
        await SeedChainAsync(factory, 3, action: "Vote.Closed");

        const string Query = "action=Vote.Closed&entityType=Topic";
        var client = Client(factory, "Auditor");

        var exported = JsonSerializer.Deserialize<List<ExportRow>>(
            await (await client.GetAsync($"/api/audit/export?format=json&{Query}")).Content.ReadAsStringAsync(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        using var doc = JsonDocument.Parse(
            await (await client.GetAsync($"/api/audit?{Query}")).Content.ReadAsStringAsync());
        var registerTotal = doc.RootElement.GetProperty("total").GetInt32();

        exported.Should().HaveCount(3);
        exported.Should().HaveCount(registerTotal, "a filter must mean the same thing on screen and in the file");
        exported.Should().OnlyContain(r => r.Action == "Vote.Closed");
    }
}
