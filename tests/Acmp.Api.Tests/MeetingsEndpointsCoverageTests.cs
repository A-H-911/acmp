using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Acmp.Api.Tests;

// HTTP coverage for the agenda-building and conduct endpoints not exercised by MeetingsApiTests:
// cancel, agenda move/timebox/presenter, and the live-meeting attendance/discussion/actual-time.
// Drives the real lifecycle (schedule -> build -> publish -> start -> conduct -> end) over HTTP.
public class MeetingsEndpointsCoverageTests
{
    private static HttpClient Client(AcmpWebApplicationFactory factory, string? roles, string sub = "kc-sec")
    {
        var client = factory.CreateClient();
        if (roles is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
            client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        }
        return client;
    }

    private static object ScheduleBody() => new
    {
        title = "Weekly Architecture Committee",
        chairUserId = Guid.NewGuid(),
        chairName = "Sara Chair",
        scheduledStart = DateTimeOffset.Parse("2026-07-01T09:00:00Z"),
        scheduledEnd = DateTimeOffset.Parse("2026-07-01T10:30:00Z"),
        location = (string?)null,
        joinUrl = (string?)null,
    };

    private static object AgendaItemBody(Guid topicId, string key, string title) => new
    {
        topicId,
        topicKey = key,
        topicTitle = title,
        urgent = false,
        timeboxMinutes = 15,
        presenterUserId = Guid.NewGuid(),
        presenterName = "Omar H.",
    };

    private sealed record MeetingSummary(Guid Id, string Key, string Status, string AgendaStatus);
    private sealed record AgendaInfo(string Status);
    private sealed record MeetingDetail(string Key, string Status, AgendaInfo? Agenda);

    private static async Task<MeetingSummary> ScheduleAsync(HttpClient sec)
    {
        var resp = await sec.PostAsJsonAsync("/api/meetings", ScheduleBody());
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await resp.Content.ReadFromJsonAsync<MeetingSummary>())!;
    }

    [Fact] // W6: agenda building on a Draft agenda — move / timebox / presenter
    public async Task Secretary_builds_the_draft_agenda_with_move_timebox_and_presenter()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary");
        var meeting = await ScheduleAsync(sec);

        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        (await sec.PostAsJsonAsync($"/api/meetings/{meeting.Id}/agenda/items", AgendaItemBody(t1, "TOP-2026-001", "Adopt Keycloak")))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await sec.PostAsJsonAsync($"/api/meetings/{meeting.Id}/agenda/items", AgendaItemBody(t2, "TOP-2026-002", "Pick a queue")))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // move the second item up
        (await sec.PostAsJsonAsync($"/api/meetings/{meeting.Id}/agenda/items/{t2}/move", new { delta = -1 }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        // retimebox the first item
        (await sec.PostAsJsonAsync($"/api/meetings/{meeting.Id}/agenda/items/{t1}/timebox", new { minutes = 25 }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        // reassign the presenter on the first item
        (await sec.PostAsJsonAsync($"/api/meetings/{meeting.Id}/agenda/items/{t1}/presenter",
            new { presenterUserId = Guid.NewGuid(), presenterName = "Lina K." }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact] // W7/W8/W9: conduct the meeting — attendance, discussion, actual-time, end
    public async Task Secretary_conducts_a_published_meeting_end_to_end()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary");
        var meeting = await ScheduleAsync(sec);

        var topicId = Guid.NewGuid();
        (await sec.PostAsJsonAsync($"/api/meetings/{meeting.Id}/agenda/items", AgendaItemBody(topicId, "TOP-2026-001", "Adopt Keycloak")))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await sec.PostAsync($"/api/meetings/{meeting.Id}/agenda/publish", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await sec.PostAsync($"/api/meetings/{meeting.Id}/start", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await sec.PostAsJsonAsync($"/api/meetings/{meeting.Id}/attendance",
            new { userId = Guid.NewGuid(), name = "Omar H.", role = "Member", status = "Present", isVotingEligible = true }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await sec.PostAsJsonAsync($"/api/meetings/{meeting.Id}/discussion",
            new { topicId, body = "Agreed to adopt Keycloak; document the rollback path." }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await sec.PostAsJsonAsync($"/api/meetings/{meeting.Id}/agenda/items/{topicId}/actual-time",
            new { actualMinutes = 12, outcome = "Discussed" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await sec.PostAsync($"/api/meetings/{meeting.Id}/end", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact] // W5: cancel a scheduled meeting with a reason
    public async Task Secretary_cancels_a_scheduled_meeting()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary");
        var meeting = await ScheduleAsync(sec);

        var cancel = await sec.PostAsJsonAsync($"/api/meetings/{meeting.Id}/cancel", new { reason = "Quorum will not be met." });
        cancel.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await (await sec.GetAsync($"/api/meetings/{meeting.Key}")).Content.ReadFromJsonAsync<MeetingDetail>();
        detail!.Status.Should().Be("Cancelled");
    }

    [Fact] // docs/10: cancel is Chairman/Secretary — a Member is forbidden
    public async Task Member_cannot_cancel_a_meeting_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var meeting = await ScheduleAsync(Client(factory, "Secretary"));

        var cancel = await Client(factory, "Member", sub: "kc-omar")
            .PostAsJsonAsync($"/api/meetings/{meeting.Id}/cancel", new { reason = "no" });
        cancel.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
