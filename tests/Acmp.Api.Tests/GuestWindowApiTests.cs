using System.Net;
using System.Net.Http.Json;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Api.Tests;

// DW-025 / DEC-041 — guest access follows the meeting.
//
// A guest's window is granted for ONE slot. When that slot stops existing, the access granted for it
// stops too: cancelling the meeting, removing the item, or handing the slot to somebody else. The
// guest here is created by the real invite, so what is asserted is the actual stored column moving.
//
// ⚠ RESCHEDULE IS NOT COVERED BECAUSE IT DOES NOT EXIST. Meeting.ScheduledStart/End are private-set
// and assigned only in the Schedule factory; there is no PUT/PATCH on /api/meetings and no feature
// mutates them. DW-025 as raised assumed otherwise. IGuestWindowWriter takes an arbitrary instant so
// a future reschedule can call it with the new end plus the grace.
public class GuestWindowApiTests
{
    private static readonly DateTimeOffset FutureEnd = DateTimeOffset.Parse("2099-07-01T10:30:00Z");

    private static HttpClient Client(AcmpWebApplicationFactory factory, string roles, string sub)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        return client;
    }

    private sealed record MeetingSummary(Guid Id, string Key);
    private sealed record InvitedGuest(Guid PublicId, string Email, DateTimeOffset AccessExpiresAt);

    private static async Task<Guid> ScheduleAsync(HttpClient sec)
    {
        var scheduled = await sec.PostAsJsonAsync("/api/meetings", new
        {
            title = "Weekly Architecture Committee",
            chairUserId = Guid.NewGuid(),
            chairName = "Sara Chair",
            scheduledStart = FutureEnd.AddHours(-1),
            scheduledEnd = FutureEnd,
            location = (string?)null,
            joinUrl = (string?)null,
        });
        scheduled.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await scheduled.Content.ReadFromJsonAsync<MeetingSummary>())!.Id;
    }

    private static async Task AddItemAsync(HttpClient sec, Guid meetingId, Guid topicId, string key) =>
        (await sec.PostAsJsonAsync($"/api/meetings/{meetingId}/agenda/items", new
        {
            topicId,
            topicKey = key,
            topicTitle = "A topic",
            urgent = false,
            timeboxMinutes = 15,
            presenterUserId = (Guid?)null,
            presenterName = (string?)null,
        })).StatusCode.Should().Be(HttpStatusCode.OK);

    private static async Task<InvitedGuest> InviteAsync(HttpClient sec, Guid meetingId, Guid topicId, string email) =>
        (await (await sec.PostAsJsonAsync($"/api/meetings/{meetingId}/guest-presenters",
            new { topicId, email, fullName = "Nadia Presenter" })).Content.ReadFromJsonAsync<InvitedGuest>())!;

    private static async Task<DateTimeOffset?> WindowOf(AcmpWebApplicationFactory factory, Guid publicId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MembershipDbContext>();
        return (await db.Members.AsNoTracking().FirstAsync(m => m.PublicId == publicId)).AccessExpiresAt;
    }

    [Fact] // a meeting that will not happen must not leave an outsider with live access
    public async Task Cancelling_a_meeting_closes_its_guest_presenters_windows()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var sec = Client(factory, "Secretary", "kc-sec");
        var meetingId = await ScheduleAsync(sec);
        var topicId = Guid.NewGuid();
        await AddItemAsync(sec, meetingId, topicId, "TOP-2026-030");
        var guest = await InviteAsync(sec, meetingId, topicId, "cancel@vendor.example");

        (await WindowOf(factory, guest.PublicId)).Should().Be(FutureEnd + Modules.Meetings.Application.Features.InviteGuestPresenter.GuestAccess.Grace);

        var cancelled = await sec.PostAsJsonAsync($"/api/meetings/{meetingId}/cancel", new { reason = "Quorum lost" });
        cancelled.IsSuccessStatusCode.Should().BeTrue();

        // Closed, not merely shortened: the window is now in the past, so the very next request is
        // refused by the ADR-0039 middleware.
        var after = await WindowOf(factory, guest.PublicId);
        after.Should().NotBeNull();
        after.Should().BeBefore(DateTimeOffset.UtcNow.AddSeconds(1));

        var refused = await Client(factory, "Guest", $"kc-{guest.Email}").PostAsync("/api/members/me", content: null);
        refused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        refused.Headers.GetValues("X-Acmp-Auth-Reason").Should().ContainSingle().Which.Should().Be("access_expired");
    }

    [Fact] // the slot the access was granted for no longer exists
    public async Task Removing_the_agenda_item_closes_its_guest_presenters_window()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var sec = Client(factory, "Secretary", "kc-sec");
        var meetingId = await ScheduleAsync(sec);
        var topicId = Guid.NewGuid();
        await AddItemAsync(sec, meetingId, topicId, "TOP-2026-031");
        var guest = await InviteAsync(sec, meetingId, topicId, "removed@vendor.example");

        var removed = await sec.DeleteAsync($"/api/meetings/{meetingId}/agenda/items/{topicId}");
        removed.IsSuccessStatusCode.Should().BeTrue();

        (await WindowOf(factory, guest.PublicId)).Should().BeBefore(DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact] // handing the slot to somebody else revokes the guest it was granted for
    public async Task Reassigning_the_slot_closes_the_replaced_guests_window()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var sec = Client(factory, "Secretary", "kc-sec");
        var meetingId = await ScheduleAsync(sec);
        var topicId = Guid.NewGuid();
        await AddItemAsync(sec, meetingId, topicId, "TOP-2026-032");
        var guest = await InviteAsync(sec, meetingId, topicId, "replaced@vendor.example");

        var reassigned = await sec.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/agenda/items/{topicId}/presenter",
            new { presenterUserId = Guid.NewGuid(), presenterName = "Omar H." });
        reassigned.IsSuccessStatusCode.Should().BeTrue();

        (await WindowOf(factory, guest.PublicId)).Should().BeBefore(DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact] // ⚠ THE CASE THAT MAKES THIS NON-TRIVIAL: a guest with a SECOND slot keeps their access
    public async Task A_guest_who_still_presents_elsewhere_keeps_their_window()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var sec = Client(factory, "Secretary", "kc-sec");

        var firstMeeting = await ScheduleAsync(sec);
        var firstTopic = Guid.NewGuid();
        await AddItemAsync(sec, firstMeeting, firstTopic, "TOP-2026-033");
        var guest = await InviteAsync(sec, firstMeeting, firstTopic, "twoslots@vendor.example");

        // A second slot on another meeting, presented by the SAME person.
        var secondMeeting = await ScheduleAsync(sec);
        var secondTopic = Guid.NewGuid();
        await AddItemAsync(sec, secondMeeting, secondTopic, "TOP-2026-034");
        (await sec.PostAsJsonAsync($"/api/meetings/{secondMeeting}/agenda/items/{secondTopic}/presenter",
            new { presenterUserId = guest.PublicId, presenterName = "Nadia Presenter" }))
            .IsSuccessStatusCode.Should().BeTrue();

        var before = await WindowOf(factory, guest.PublicId);
        (await sec.PostAsJsonAsync($"/api/meetings/{firstMeeting}/cancel", new { reason = "Quorum lost" }))
            .IsSuccessStatusCode.Should().BeTrue();

        // Untouched. Closing it would revoke access they still need for the slot they still present.
        (await WindowOf(factory, guest.PublicId)).Should().Be(before);
    }

    [Fact] // an ORDINARY member presenting at a cancelled meeting must not acquire an expiry
    public async Task Cancelling_does_not_give_a_committee_member_an_access_window()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        await factory.SeedMembersAsync(("kc-omar", "Omar H", Modules.Membership.Domain.Enums.CommitteeRole.Member));
        Guid memberId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MembershipDbContext>();
            memberId = (await db.Members.FirstAsync(m => m.KeycloakUserId == "kc-omar")).PublicId;
        }

        var sec = Client(factory, "Secretary", "kc-sec");
        var meetingId = await ScheduleAsync(sec);
        var topicId = Guid.NewGuid();
        await AddItemAsync(sec, meetingId, topicId, "TOP-2026-035");
        (await sec.PostAsJsonAsync($"/api/meetings/{meetingId}/agenda/items/{topicId}/presenter",
            new { presenterUserId = memberId, presenterName = "Omar H" })).IsSuccessStatusCode.Should().BeTrue();

        (await sec.PostAsJsonAsync($"/api/meetings/{meetingId}/cancel", new { reason = "Quorum lost" }))
            .IsSuccessStatusCode.Should().BeTrue();

        // Still null. A member handed an expiry by accident would be locked out of the whole product,
        // and DEF-029 means the row could never be deleted to recover.
        (await WindowOf(factory, memberId)).Should().BeNull();
    }
}
