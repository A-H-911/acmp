using System.Net;
using System.Net.Http.Json;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Infrastructure.Audit;
using Acmp.Shared.Infrastructure.Configuration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Api.Tests;

// C-INS-01 / NFR-065 (WBS-26.2): the two insider-threat anomaly signals, each FORCED to fire and asserted
// AS A ROW, each with a negative control.
//
// ⚠⚠ THE NEGATIVE CONTROLS ARE NOT SYMMETRY FOR ITS OWN SAKE. A detector with no negative case cannot be
// distinguished from one that always fires, and "always fires" is the failure mode that teaches a reviewer
// to ignore the signal. Each control also asserts an ORDINARY row, so a NotContain cannot pass vacuously
// over an empty list - RefusalAuditTests records that exact failure, where two NotContain assertions passed
// the whole time against a helper reading the wrong column.
public class AnomalySignalAuditTests : IClassFixture<AcmpWebApplicationFactory>
{
    // WBS-27.2 / DEC-124 d1 - ONE host per class instead of one per test method. Every method
    // below now shares this host's fourteen InMemory databases, so a test that asserts over a
    // global count sees what its siblings wrote. SharedHostOrderGuard is the control for that.
    private readonly AcmpWebApplicationFactory _factory;

    public AnomalySignalAuditTests(AcmpWebApplicationFactory factory) => _factory = factory.Reset();

    private static HttpClient Client(AcmpWebApplicationFactory factory, string roles, string sub)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        return client;
    }

    // ⚠ `Action ?? EventType` — the coalesce RefusalAuditTests documents. Selecting Action alone returns null
    // for every lean v1 row, which reads exactly like the feature being absent.
    private static async Task<IReadOnlyList<string>> ActionsAsync(AcmpWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        return await audit.AuditEvents.Select(e => e.Action ?? e.EventType).ToListAsync();
    }

    // Thresholds are CONFIGURED rather than defaulted, which is the point of DEC-099 d3 as much as a test
    // convenience: if the value could not be set without a deployment, DEF-110's shape would be reproduced
    // here. Setting it to 1 makes a single action atypical, so the signal is forced rather than approximated.
    private static async Task ThresholdAsync(AcmpWebApplicationFactory factory, string key, int value)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
        db.Settings.Add(ConfigurationSetting.Create(key, value.ToString(), "anomaly"));
        await db.SaveChangesAsync();
    }

    private static async Task<string> RestrictedTopicKeyAsync(AcmpWebApplicationFactory factory)
    {
        await factory.SeedMembersAsync(("kc-owner", "Olive Owner", CommitteeRole.Member));
        var sec = Client(factory, "Secretary", "kc-sec");

        var submit = await Client(factory, "Member", "kc-submitter").PostAsJsonAsync("/api/topics", new
        {
            title = "Adopt Keycloak",
            description = "Consolidate IAM onto Keycloak.",
            justification = "Fragmented auth is risky.",
            type = "ArchitectureDecision",
            urgency = "Urgent",
            source = "CommitteeMember",
            streams = new[] { "core" },
            systems = Array.Empty<string>(),
            tags = Array.Empty<string>(),
        });
        submit.StatusCode.Should().Be(HttpStatusCode.Created);
        var topic = await submit.Content.ReadFromJsonAsync<SubmitResult>();

        (await sec.PutAsJsonAsync($"/api/topics/{topic!.Id}/confidentiality", new { restricted = true }))
            .IsSuccessStatusCode.Should().BeTrue();
        return topic.Key;
    }

    [Fact] // NFR-065 signal 1: an export at or above the threshold leaves a bulk-export anomaly ROW
    public async Task A_large_audit_export_leaves_a_bulk_export_anomaly_row()
    {
        var factory = _factory;
        await ThresholdAsync(factory, AnomalyDetector.BulkExportRowsKey, 1);

        // ⚠ A PRIOR *READ* WRITES NOTHING, AND THE FIRST VERSION OF THIS TEST ASSUMED IT DID. Reads are not
        // audited in this codebase - that absence is the very gap this item closes for Restricted topics -
        // so the export delivered ZERO rows and the threshold of 1 was never reached. A WRITE is needed to
        // put a row in the log. The test failed loudly rather than passing over an empty export.
        var sec = Client(factory, "Secretary", "kc-sec");
        (await Client(factory, "Member", "kc-submitter").PostAsJsonAsync("/api/topics", new
        {
            title = "Adopt Keycloak",
            description = "Consolidate IAM onto Keycloak.",
            justification = "Fragmented auth is risky.",
            type = "ArchitectureDecision",
            urgency = "Urgent",
            source = "CommitteeMember",
            streams = new[] { "core" },
            systems = Array.Empty<string>(),
            tags = Array.Empty<string>(),
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        (await sec.GetAsync("/api/audit/export?format=json")).IsSuccessStatusCode.Should().BeTrue();

        var actions = await ActionsAsync(factory);
        actions.Should().Contain(AnomalyDetector.BulkExportAnomalyEvent);
        actions.Should().Contain("audit.exported", "the C-AUDIT-08 record is a SEPARATE event, not replaced");
    }

    [Fact] // the control: below the threshold, the export is audited and NO anomaly row appears
    public async Task A_small_audit_export_leaves_no_anomaly_row()
    {
        var factory = _factory;
        await ThresholdAsync(factory, AnomalyDetector.BulkExportRowsKey, 100_000);

        var sec = Client(factory, "Secretary", "kc-sec");
        (await sec.GetAsync("/api/audit/export?format=json")).IsSuccessStatusCode.Should().BeTrue();

        var actions = await ActionsAsync(factory);
        // The ordinary row proves the audit path ran at all, so the absence below means the RULE declined
        // to fire rather than nothing being written.
        actions.Should().Contain("audit.exported");
        actions.Should().NotContain(AnomalyDetector.BulkExportAnomalyEvent);
    }

    [Fact] // NFR-065 signal 2, and the DATA half DEC-099 d1 found missing entirely
    public async Task Reading_a_restricted_topic_leaves_an_access_row()
    {
        var factory = _factory;
        await ThresholdAsync(factory, AnomalyDetector.RestrictedAccessCountKey, 100_000);
        var topicKey = await RestrictedTopicKeyAsync(factory);

        var sec = Client(factory, "Secretary", "kc-sec");
        var detail = await sec.GetAsync($"/api/topics/{topicKey}");
        detail.IsSuccessStatusCode.Should().BeTrue();

        var actions = await ActionsAsync(factory);
        // ⚠ THIS IS THE ASSERTION THAT WOULD HAVE FAILED BEFORE THIS ITEM, and it is the whole reason the
        // item grew: measured 2026-08-30, 18 write features in Topics audited and ZERO read features did.
        actions.Should().Contain(AnomalyDetector.AccessEvent);
        actions.Should().NotContain(AnomalyDetector.RestrictedAccessAnomalyEvent, "one read is not atypical");
    }

    [Fact] // NFR-065 signal 2: repeated Restricted reads by one principal leave an anomaly ROW
    public async Task Repeated_restricted_topic_reads_leave_an_anomaly_row()
    {
        var factory = _factory;
        await ThresholdAsync(factory, AnomalyDetector.RestrictedAccessCountKey, 2);
        var topicKey = await RestrictedTopicKeyAsync(factory);

        var sec = Client(factory, "Secretary", "kc-sec");
        for (var i = 0; i < 3; i++)
            (await sec.GetAsync($"/api/topics/{topicKey}")).IsSuccessStatusCode.Should().BeTrue();

        (await ActionsAsync(factory)).Should().Contain(AnomalyDetector.RestrictedAccessAnomalyEvent);
    }

    [Fact] // DEF-124 / AC-157: a read the caller was not permitted must leave NO access row
    public async Task A_refused_restricted_topic_read_leaves_no_access_row()
    {
        var factory = _factory;
        var topicKey = await RestrictedTopicKeyAsync(factory);

        // A Member who is not a grantee. The refusal is a 404, NOT a 403 — a 403 would itself confirm
        // that the key names a real topic, which is the fact the classification protects.
        var outsider = Client(factory, "Member", "kc-outsider");
        (await outsider.GetAsync($"/api/topics/{topicKey}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var actions = await ActionsAsync(factory);

        // ⚠⚠ THE POSITIVE HALF IS NOT DECORATION. RefusalAuditTests records two NotContain assertions
        // that passed the whole time against an empty list; asserting an ordinary row first proves the
        // audit store was reachable and populated, so the NotContain below cannot pass vacuously.
        actions.Should().NotBeEmpty();

        // ⛔ AC-157's placement clause, at the API layer. The handler test pins the ORDER against a
        // substitute; this pins the PATH — LL-030 says a defence layer needs both, because a front-door
        // test cannot see the ordering and a handler test cannot prove the route reaches it.
        actions.Should().NotContain(AnomalyDetector.AccessEvent,
            "recording above the visibility check would log a read that never happened and leak, into the "
            + "audit log, the existence of a topic the classification exists to hide");
        actions.Should().NotContain(AnomalyDetector.RestrictedAccessAnomalyEvent);
    }

    private sealed record SubmitResult(Guid Id, string Key);
}
