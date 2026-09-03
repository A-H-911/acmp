using System.Net;
using System.Net.Http.Json;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Infrastructure.Audit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Api.Tests;

// C-AUTH-05 SoD-4 / NFR-064, WARN-AND-AUDIT (DEC-095 d1): a decision recorded by the owner of its own topic
// is ALLOWED, flagged on the decision, and recorded as a distinct AuditEvent.
//
// ⚠⚠ THESE ASSERT THE AUDIT EVENT AS A ROW, NOT AS AN EMISSION, AND THAT IS THE REQUIREMENT'S OWN BAR.
// NFR-064's verification detail reads: "for a rule recorded as warn-and-audit - asserts the warning AND the
// resulting AuditEvent as a ROW". A mock assertion (`audit.Received(1).EmitEnrichedAsync(...)`) proves the
// handler CALLED the sink; it cannot prove a row was written, and it passes just as happily if the sink is
// misconfigured, the column is wrong, or the write is rolled back. SoD-2's test asserts the call; this one
// asserts the row, and DEC-096 d3 put the whole five-rule evidence set inside this item for that reason.
public class Sod4ConflictAuditTests : IClassFixture<AcmpWebApplicationFactory>
{
    // WBS-27.2 / DEC-124 d1 - ONE host per class instead of one per test method. Every method
    // below now shares this host's fourteen InMemory databases, so a test that asserts over a
    // global count sees what its siblings wrote. SharedHostOrderGuard is the control for that.
    private readonly AcmpWebApplicationFactory _factory;

    public Sod4ConflictAuditTests(AcmpWebApplicationFactory factory) => _factory = factory.Reset();

    private const string ConflictEvent = "Decisions.DecisionRecordedByConflictedActor";

    private static HttpClient Client(AcmpWebApplicationFactory factory, string roles, string sub)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        return client;
    }

    // ⚠ `Action ?? EventType` — the same coalesce RefusalAuditTests documents at length. The store holds two
    // row shapes and selecting Action alone returns null for every lean v1 row, which reads exactly like the
    // feature being absent. /api/audit normalizes identically, so this asserts what a reader actually sees.
    private static async Task<IReadOnlyList<string>> ActionsAsync(AcmpWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        return await audit.AuditEvents.Select(e => e.Action ?? e.EventType).ToListAsync();
    }

    private static object RecordBody(Guid topicId) => new
    {
        topicId,
        meetingId = (Guid?)null,
        outcome = "Approved",
        title = new { en = "Adopt the platform", ar = "اعتماد المنصة" },
        statement = new { en = "The committee adopts it.", ar = "تعتمدها اللجنة." },
        rationale = new { en = "It meets the brief.", ar = "تفي بالمتطلبات." },
        alternatives = (object?)null,
        voteId = (Guid?)null,
        conditions = Array.Empty<object>(),
    };

    private const string OwnerSub = "kc-olive";     // seeded Secretary, and the topic's OWNER
    private const string OtherSub = "kc-oscar";     // seeded Chairman, owns nothing - the control actor

    private static object SubmitBody() => new
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
    };

    // Seeds BOTH actors, submits a topic, and accepts it with OwnerSub as owner. Returns the topic id.
    //
    // ⚠ TWO MEMBERS ARE SEEDED ON PURPOSE, AND THE SECOND IS WHAT MAKES THE CONTROL TEST WORTH ANYTHING.
    // RecorderConflict short-circuits to "no conflict" when the actor has no committee member row at all,
    // so a control actor who was never seeded would produce the right answer for the WRONG REASON - it
    // would never reach the owner comparison this rule is about. Both actors resolve; only the ownership
    // differs.
    //
    // Both are seeded into roles that may record a decision (Secretary/Chairman), because the scenario needs
    // one person who is BOTH permitted to record and the topic's owner - exactly the legitimate overlap
    // DEC-095 d1 declined to refuse.
    private static async Task<Guid> OwnedTopicAsync(AcmpWebApplicationFactory factory)
    {
        await factory.SeedMembersAsync(
            (OwnerSub, "Olive Owner", CommitteeRole.Secretary),
            (OtherSub, "Oscar Other", CommitteeRole.Chairman));

        var submit = await Client(factory, "Member", "kc-submitter")
            .PostAsJsonAsync("/api/topics", SubmitBody());
        submit.StatusCode.Should().Be(HttpStatusCode.Created);
        var topic = await submit.Content.ReadFromJsonAsync<SubmitResult>();

        var sec = Client(factory, "Secretary", "kc-sec");
        var members = await (await sec.GetAsync("/api/members")).Content.ReadFromJsonAsync<List<MemberRow>>();
        var owner = members!.Single(m => m.Role == nameof(CommitteeRole.Secretary));

        (await sec.PostAsJsonAsync($"/api/topics/{topic!.Id}/accept",
            new { ownerId = owner.PublicId, ownerName = "Olive Owner" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        return topic.Id;
    }

    [Fact] // NFR-064 SoD-4: the overlap is ALLOWED, and it leaves a row
    public async Task Recording_a_decision_on_a_topic_you_own_is_allowed_and_leaves_a_conflict_row()
    {
        var factory = _factory;
        var topicId = await OwnedTopicAsync(factory);

        // The OWNER records the decision on their own topic.
        var response = await Client(factory, "Secretary", OwnerSub)
            .PostAsJsonAsync("/api/decisions", RecordBody(topicId));

        // WARN, NOT REFUSE. A 403 here would mean the rule was built hard, which DEC-095 d1 rejected.
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        (await ActionsAsync(factory)).Should().Contain(ConflictEvent);
    }

    [Fact] // the discriminator: a third-party recorder leaves NO conflict row
    public async Task Recording_a_decision_on_someone_elses_topic_leaves_no_conflict_row()
    {
        var factory = _factory;
        var topicId = await OwnedTopicAsync(factory);

        // A DIFFERENT seeded member records it. Same endpoint, same body, same topic - only the actor
        // differs, which is what makes this a control on the test above rather than a second happy path.
        var response = await Client(factory, "Chairman", OtherSub)
            .PostAsJsonAsync("/api/decisions", RecordBody(topicId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var actions = await ActionsAsync(factory);
        actions.Should().NotContain(ConflictEvent);
        // ⚠ THE CONTROL ON THE CONTROL. A NotContain over an EMPTY list passes vacuously - RefusalAuditTests
        // records that exact failure, where two NotContain assertions passed the whole time against a
        // helper reading the wrong column. Asserting the ordinary drafted row proves the audit path ran at
        // all, so "no conflict row" means the rule declined to fire rather than nothing being written.
        actions.Should().Contain("Decisions.DecisionDrafted");
    }

    [Fact] // NFR-064 SoD-4, the PRESENTER half - the other way a recorder is conflicted
    public async Task Recording_a_decision_on_a_topic_you_present_leaves_a_conflict_row()
    {
        var factory = _factory;
        var topicId = await OwnedTopicAsync(factory);

        var sec = Client(factory, "Secretary", "kc-sec");
        var members = await (await sec.GetAsync("/api/members")).Content.ReadFromJsonAsync<List<MemberRow>>();
        // OtherSub is the Chairman, and owns NOTHING - so a conflict here can only come from the presenter
        // leg. Using the owner would prove nothing new: the owner leg already fires on its own.
        var presenter = members!.Single(m => m.Role == nameof(CommitteeRole.Chairman));

        var meeting = await (await sec.PostAsJsonAsync("/api/meetings", new
        {
            title = "Weekly Architecture Committee",
            chairUserId = Guid.NewGuid(),
            chairName = "Sara Chair",
            scheduledStart = DateTimeOffset.Parse("2026-07-01T09:00:00Z"),
            scheduledEnd = DateTimeOffset.Parse("2026-07-01T10:30:00Z"),
            location = (string?)null,
            joinUrl = (string?)null,
        })).Content.ReadFromJsonAsync<MeetingSummary>();

        (await sec.PostAsJsonAsync($"/api/meetings/{meeting!.Id}/agenda/items", new
        {
            topicId,
            topicKey = "TOP-2026-001",
            topicTitle = "Adopt Keycloak",
            urgent = false,
            timeboxMinutes = 15,
            presenterUserId = presenter.PublicId,
            presenterName = "Oscar Other",
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        // The PRESENTER records the decision, ON THE MEETING. The meeting id is what makes the agenda leg
        // reachable at all - with a null meeting there is no agenda to ask about, which is why the two
        // tests above (meetingId null) leave AgendaPresenterReader unexecuted.
        var body = new
        {
            topicId,
            meetingId = (Guid?)meeting.Id,
            outcome = "Approved",
            title = new { en = "Adopt the platform", ar = "اعتماد المنصة" },
            statement = new { en = "The committee adopts it.", ar = "تعتمدها اللجنة." },
            rationale = new { en = "It meets the brief.", ar = "تفي بالمتطلبات." },
            alternatives = (object?)null,
            voteId = (Guid?)null,
            conditions = Array.Empty<object>(),
        };

        var response = await Client(factory, "Chairman", OtherSub).PostAsJsonAsync("/api/decisions", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        (await ActionsAsync(factory)).Should().Contain(ConflictEvent);
    }

    private sealed record MeetingSummary(Guid Id, string Key, string Status);
    private sealed record SubmitResult(Guid Id, string Key);
    private sealed record MemberRow(Guid PublicId, string Role);
}
