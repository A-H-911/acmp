using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Acmp.Api.Tests;

// HTTP-contract tests for /api/meetings through the real pipeline + policy authorization (docs/10).
// Reads are by key; mutations are by the meeting's Guid id. The committee is implicit server-side
// (CON-001), so the schedule body carries no committee id.
public class MeetingsApiTests
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

    private static object AgendaItemBody(Guid topicId) => new
    {
        topicId,
        topicKey = "TOP-2026-001",
        topicTitle = "Adopt Keycloak",
        urgent = false,
        timeboxMinutes = 15,
        presenterUserId = Guid.NewGuid(), // every item needs a presenter before the agenda can publish
        presenterName = "Omar H.",
    };

    private sealed record MeetingSummary(Guid Id, string Key, string Status, string AgendaStatus);
    private sealed record AgendaResult(string Status, int Version);
    private sealed record AgendaInfo(string Status);
    private sealed record MeetingDetail(string Key, AgendaInfo? Agenda);

    [Fact] // AC-008
    public async Task Schedule_without_token_returns_401()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, roles: null).PostAsJsonAsync("/api/meetings", ScheduleBody());
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // docs/10: Meeting.Schedule is Chairman/Secretary only
    public async Task Member_cannot_schedule_a_meeting_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, "Member").PostAsJsonAsync("/api/meetings", ScheduleBody());
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // W5: schedule → list → detail (with a Draft agenda) → unknown key 404
    public async Task Secretary_schedules_then_reads_list_and_detail()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", sub: "kc-sec");

        var schedule = await sec.PostAsJsonAsync("/api/meetings", ScheduleBody());
        schedule.StatusCode.Should().Be(HttpStatusCode.Created);
        var meeting = await schedule.Content.ReadFromJsonAsync<MeetingSummary>();
        meeting!.Key.Should().Be("MTG-2026-001");
        meeting.Status.Should().Be("Scheduled");
        meeting.AgendaStatus.Should().Be("Draft");

        var list = await (await sec.GetAsync("/api/meetings")).Content.ReadFromJsonAsync<List<MeetingSummary>>();
        list!.Should().ContainSingle().Which.Key.Should().Be("MTG-2026-001");

        var detail = await sec.GetAsync($"/api/meetings/{meeting.Key}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        (await detail.Content.ReadFromJsonAsync<MeetingDetail>())!.Agenda!.Status.Should().Be("Draft");

        var missing = await sec.GetAsync("/api/meetings/MTG-2026-999");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact] // W6: build the agenda then publish it (the agenda flips Draft → Published, version 1)
    public async Task Secretary_adds_an_item_then_publishes_the_agenda()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var sec = Client(factory, "Secretary", sub: "kc-sec");
        var meeting = await (await sec.PostAsJsonAsync("/api/meetings", ScheduleBody())).Content.ReadFromJsonAsync<MeetingSummary>();

        var add = await sec.PostAsJsonAsync($"/api/meetings/{meeting!.Id}/agenda/items", AgendaItemBody(Guid.NewGuid()));
        add.StatusCode.Should().Be(HttpStatusCode.OK);

        var publish = await sec.PostAsync($"/api/meetings/{meeting.Id}/agenda/publish", null);
        publish.StatusCode.Should().Be(HttpStatusCode.OK);
        var agenda = await publish.Content.ReadFromJsonAsync<AgendaResult>();
        agenda!.Status.Should().Be("Published");
        agenda.Version.Should().Be(1);
    }

    [Fact] // docs/10: agenda publish is Chairman/Secretary — a Member is forbidden
    public async Task Member_cannot_publish_an_agenda_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var meeting = await (await Client(factory, "Secretary", sub: "kc-sec")
            .PostAsJsonAsync("/api/meetings", ScheduleBody())).Content.ReadFromJsonAsync<MeetingSummary>();

        var publish = await Client(factory, "Member").PostAsync($"/api/meetings/{meeting!.Id}/agenda/publish", null);
        publish.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
