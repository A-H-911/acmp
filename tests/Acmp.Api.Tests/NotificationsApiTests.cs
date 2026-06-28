using System.Net;
using System.Net.Http.Json;
using Acmp.Modules.Membership.Domain.Enums;
using FluentAssertions;

namespace Acmp.Api.Tests;

// End-to-end HTTP tests for the in-app notification floor (AC-051/053): publishing an agenda fans out a
// real notification to every active committee member (resolved via the cross-module directory), and each
// member reads / marks-read only their own feed. Exercises the full pipeline + the three modules
// (Meetings → Membership directory → Notifications) wired through Acmp.Shared contracts.
public class NotificationsApiTests
{
    private static HttpClient Client(AcmpWebApplicationFactory factory, string? roles, string sub)
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

    private sealed record MeetingSummary(Guid Id, string Key);
    private sealed record NotificationDto(Guid Id, string TitleEn, string BodyEn, string Category, string? DeepLink, bool IsRead);
    private sealed record NotificationList(List<NotificationDto> Items, int UnreadCount);

    // Secretary schedules a meeting, places one item, and publishes the agenda → fans out notifications.
    private static async Task<string> PublishAgendaAsync(AcmpWebApplicationFactory factory)
    {
        var sec = Client(factory, "Secretary", sub: "kc-sec");
        var meeting = await (await sec.PostAsJsonAsync("/api/meetings", ScheduleBody())).Content.ReadFromJsonAsync<MeetingSummary>();
        await sec.PostAsJsonAsync($"/api/meetings/{meeting!.Id}/agenda/items", new
        {
            topicId = Guid.NewGuid(),
            topicKey = "TOP-2026-001",
            topicTitle = "Adopt Keycloak",
            urgent = false,
            timeboxMinutes = 15,
            presenterUserId = Guid.NewGuid(), // every item needs a presenter before the agenda can publish
            presenterName = "Omar H.",
        });
        (await sec.PostAsync($"/api/meetings/{meeting.Id}/agenda/publish", null)).EnsureSuccessStatusCode();
        return meeting.Key;
    }

    [Fact] // AC-008
    public async Task Notifications_without_token_returns_401()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await factory.CreateClient().GetAsync("/api/notifications");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // AC-051: publishing notifies every active committee member with date + agenda title + a deep link
    public async Task Publishing_an_agenda_notifies_committee_members()
    {
        await using var factory = new AcmpWebApplicationFactory();
        // Seed TWO members so the fan-out exercises the multi-recipient path (a single recipient hid the
        // owned-LocalizedString sharing bug that 500'd the 2nd notification).
        await factory.SeedMembersAsync(
            ("kc-omar", "Omar H.", CommitteeRole.Member),
            ("kc-lena", "Lena K.", CommitteeRole.Member));

        var key = await PublishAgendaAsync(factory);

        var feed = await (await Client(factory, "Member", sub: "kc-omar").GetAsync("/api/notifications"))
            .Content.ReadFromJsonAsync<NotificationList>();

        feed!.UnreadCount.Should().BeGreaterThan(0);
        var published = feed.Items.Should().ContainSingle(n => n.Category == "AgendaPublished").Subject;
        published.DeepLink.Should().Be($"/meetings/{key}");
        published.TitleEn.Should().NotBeNullOrWhiteSpace();
        published.BodyEn.Should().Contain("Weekly Architecture Committee"); // the agenda title
        published.IsRead.Should().BeFalse();
    }

    [Fact] // AC-053 path + IDOR guard: a member marks only their own notification read
    public async Task Mark_read_is_scoped_to_the_caller()
    {
        await using var factory = new AcmpWebApplicationFactory();
        // Seed TWO members so the fan-out exercises the multi-recipient path (a single recipient hid the
        // owned-LocalizedString sharing bug that 500'd the 2nd notification).
        await factory.SeedMembersAsync(
            ("kc-omar", "Omar H.", CommitteeRole.Member),
            ("kc-lena", "Lena K.", CommitteeRole.Member));
        await PublishAgendaAsync(factory);

        var omar = Client(factory, "Member", sub: "kc-omar");
        var before = await (await omar.GetAsync("/api/notifications")).Content.ReadFromJsonAsync<NotificationList>();
        var target = before!.Items.First(n => n.Category == "AgendaPublished");

        // A different user cannot mark Omar's notification → 404 (no existence leak), and it stays unread.
        var asOther = await Client(factory, "Member", sub: "kc-stranger").PostAsync($"/api/notifications/{target.Id}/read", null);
        asOther.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Omar marks his own → 204, and the unread count drops.
        var asOwner = await omar.PostAsync($"/api/notifications/{target.Id}/read", null);
        asOwner.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await (await omar.GetAsync("/api/notifications")).Content.ReadFromJsonAsync<NotificationList>();
        after!.UnreadCount.Should().Be(before.UnreadCount - 1);
        after.Items.Single(n => n.Id == target.Id).IsRead.Should().BeTrue();
    }

    [Fact] // an unknown notification id → 404
    public async Task Mark_unknown_notification_returns_404()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, "Member", sub: "kc-omar").PostAsync($"/api/notifications/{Guid.NewGuid()}/read", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact] // full-page center (#79): GET pages the feed, and POST /read-all clears the caller's unread
    public async Task Get_supports_paging_and_read_all_clears_unread()
    {
        await using var factory = new AcmpWebApplicationFactory();
        await factory.SeedMembersAsync(
            ("kc-omar", "Omar H.", CommitteeRole.Member),
            ("kc-lena", "Lena K.", CommitteeRole.Member));
        await PublishAgendaAsync(factory);
        await PublishAgendaAsync(factory); // two publishes → two notifications for Omar

        var omar = Client(factory, "Member", sub: "kc-omar");

        // pageSize=1 returns a single item even though more exist; the unread total is the full count
        // (the page does not cap the badge), so paging is observable.
        var page1 = await (await omar.GetAsync("/api/notifications?page=1&pageSize=1")).Content.ReadFromJsonAsync<NotificationList>();
        page1!.Items.Should().HaveCount(1);
        page1.UnreadCount.Should().BeGreaterThan(1);

        // mark all read → the caller's unread count drops to zero.
        (await omar.PostAsync("/api/notifications/read-all", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await (await omar.GetAsync("/api/notifications")).Content.ReadFromJsonAsync<NotificationList>();
        after!.UnreadCount.Should().Be(0);
    }
}
