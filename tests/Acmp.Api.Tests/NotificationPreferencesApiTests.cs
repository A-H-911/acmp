using System.Net;
using System.Net.Http.Json;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Infrastructure.Audit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Api.Tests;

// FR-133 / AC-160 (DEC-186) over HTTP: GET/PUT /api/notifications/preferences are the caller's own, an
// opt-out withholds that member's in-app notification end to end (Meetings → directory → Notifications)
// while the act's audit row is written as before, and turning it back on delivers LATER events only.
public class NotificationPreferencesApiTests : IClassFixture<AcmpWebApplicationFactory>
{
    private readonly AcmpWebApplicationFactory _factory;

    public NotificationPreferencesApiTests(AcmpWebApplicationFactory factory) => _factory = factory.Reset();

    private HttpClient Client(string roles, string sub)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        return client;
    }

    private sealed record Pref(string Category, string Group, bool InApp);
    private sealed record Prefs(List<Pref> Items);
    private sealed record MeetingSummary(Guid Id, string Key);
    private sealed record NotificationDto(string Category);
    private sealed record NotificationList(List<NotificationDto> Items);

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, params (string Category, bool InApp)[] items) =>
        client.PutAsJsonAsync("/api/notifications/preferences",
            new { items = items.Select(i => new { category = i.Category, inApp = i.InApp }) });

    private async Task PublishAgendaAsync()
    {
        var sec = Client("Secretary", "kc-sec");
        var meeting = await (await sec.PostAsJsonAsync("/api/meetings", new
        {
            title = "Weekly Architecture Committee",
            chairUserId = Guid.NewGuid(),
            chairName = "Sara Chair",
            scheduledStart = DateTimeOffset.Parse("2026-10-01T09:00:00Z"),
            scheduledEnd = DateTimeOffset.Parse("2026-10-01T10:30:00Z"),
        })).Content.ReadFromJsonAsync<MeetingSummary>();
        await sec.PostAsJsonAsync($"/api/meetings/{meeting!.Id}/agenda/items", new
        {
            topicId = Guid.NewGuid(),
            topicKey = "TOP-2026-001",
            topicTitle = "Adopt Keycloak",
            urgent = false,
            timeboxMinutes = 15,
            presenterUserId = Guid.NewGuid(),
            presenterName = "Omar H.",
        });
        (await sec.PostAsync($"/api/meetings/{meeting.Id}/agenda/publish", null)).EnsureSuccessStatusCode();
    }

    private async Task<int> AgendaNotificationsAsync(string sub) =>
        (await (await Client("Member", sub).GetAsync("/api/notifications?pageSize=50"))
            .Content.ReadFromJsonAsync<NotificationList>())!.Items.Count(n => n.Category == "AgendaPublished");

    private async Task<int> AgendaAuditRowsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        return await audit.AuditEvents.CountAsync(e => (e.Action ?? e.EventType) == "Meetings.AgendaPublished");
    }

    [Fact]
    public async Task Preferences_without_a_token_return_401()
    {
        (await _factory.CreateClient().GetAsync("/api/notifications/preferences")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _factory.CreateClient().PutAsJsonAsync("/api/notifications/preferences", new { items = Array.Empty<object>() }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_lists_all_22_event_types_on_by_default()
    {
        var prefs = await (await Client("Member", "kc-fresh").GetAsync("/api/notifications/preferences")).Content.ReadFromJsonAsync<Prefs>();
        prefs!.Items.Should().HaveCount(22).And.OnlyContain(p => p.InApp);
        prefs.Items[0].Should().Be(new Pref("MeetingScheduled", "meetings", true));
    }

    [Fact]
    public async Task A_choice_persists_for_the_caller_only_and_across_clients()
    {
        (await PutAsync(Client("Member", "kc-omar"), ("VoteOpened", false))).StatusCode.Should().Be(HttpStatusCode.OK);

        var again = await (await Client("Member", "kc-omar").GetAsync("/api/notifications/preferences")).Content.ReadFromJsonAsync<Prefs>();
        again!.Items.Single(p => p.Category == "VoteOpened").InApp.Should().BeFalse();

        var other = await (await Client("Member", "kc-lena").GetAsync("/api/notifications/preferences")).Content.ReadFromJsonAsync<Prefs>();
        other!.Items.Should().OnlyContain(p => p.InApp);
    }

    [Fact]
    public async Task An_unknown_event_type_is_a_400()
    {
        (await PutAsync(Client("Member", "kc-omar"), ("MinutesReady", false))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_opt_out_withholds_that_members_notification_and_the_audit_row_is_unaffected()
    {
        await _factory.SeedMembersAsync(("kc-omar", "Omar H.", CommitteeRole.Member), ("kc-lena", "Lena K.", CommitteeRole.Member));
        (await PutAsync(Client("Member", "kc-omar"), ("AgendaPublished", false))).EnsureSuccessStatusCode();
        var auditBefore = await AgendaAuditRowsAsync();

        await PublishAgendaAsync();

        (await AgendaNotificationsAsync("kc-omar")).Should().Be(0);
        (await AgendaNotificationsAsync("kc-lena")).Should().Be(1);
        (await AgendaAuditRowsAsync()).Should().Be(auditBefore + 1);
    }

    [Fact]
    public async Task Turning_it_back_on_delivers_later_events_and_nothing_withheld()
    {
        await _factory.SeedMembersAsync(("kc-omar", "Omar H.", CommitteeRole.Member));
        var omar = Client("Member", "kc-omar");
        (await PutAsync(omar, ("AgendaPublished", false))).EnsureSuccessStatusCode();
        await PublishAgendaAsync();

        (await PutAsync(omar, ("AgendaPublished", true))).EnsureSuccessStatusCode();
        (await AgendaNotificationsAsync("kc-omar")).Should().Be(0);

        await PublishAgendaAsync();
        (await AgendaNotificationsAsync("kc-omar")).Should().Be(1);
    }
}
