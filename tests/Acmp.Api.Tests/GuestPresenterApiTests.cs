using System.Net;
using System.Net.Http.Json;
using Acmp.Modules.Meetings.Application.Features.InviteGuestPresenter;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Shared.Infrastructure.Audit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Api.Tests;

// FR-159 / AC-092 — the guest-presenter invite, through the real pipeline (ADR-0040).
//
// THE POINT OF THIS FILE IS THAT THE WINDOW IS PROVEN BY A REFUSED REQUEST, not by reading back the
// column that was just written. Two meetings are scheduled — one whose window has already closed and
// one whose has not — and the guest created by each is sent at the API. One is refused and one is
// not, which is the only evidence that distinguishes "expiry is enforced" from "a date was stored".
public class GuestPresenterApiTests
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

    // Already over: the invite's window (end + 24h) closed well before the test runs.
    private static readonly DateTimeOffset PastEnd = DateTimeOffset.Parse("2020-07-01T10:30:00Z");
    // Comfortably ahead of any plausible run date, so the "not yet expired" leg never becomes flaky.
    private static readonly DateTimeOffset FutureEnd = DateTimeOffset.Parse("2099-07-01T10:30:00Z");

    private static object ScheduleBody(DateTimeOffset end) => new
    {
        title = "Weekly Architecture Committee",
        chairUserId = Guid.NewGuid(),
        chairName = "Sara Chair",
        scheduledStart = end.AddHours(-1),
        scheduledEnd = end,
        location = (string?)null,
        joinUrl = (string?)null,
    };

    private sealed record MeetingSummary(Guid Id, string Key);
    private sealed record InvitedGuest(Guid PublicId, string FullName, string Email, DateTimeOffset AccessExpiresAt, string TemporaryPassword);
    private sealed record AgendaItem(Guid TopicId, Guid? PresenterUserId, string? PresenterName);
    private sealed record Agenda(List<AgendaItem> Items);
    private sealed record MeetingDetail(Agenda? Agenda);

    /// <summary>Schedules a meeting with one agenda item and returns (meetingId, meetingKey, topicId).</summary>
    private static async Task<(Guid MeetingId, string Key, Guid TopicId)> ScheduleWithOneItemAsync(
        AcmpWebApplicationFactory factory, DateTimeOffset end)
    {
        var sec = Client(factory, "Secretary", sub: "kc-sec");
        var scheduled = await sec.PostAsJsonAsync("/api/meetings", ScheduleBody(end));
        scheduled.StatusCode.Should().Be(HttpStatusCode.Created);
        var meeting = (await scheduled.Content.ReadFromJsonAsync<MeetingSummary>())!;

        var topicId = Guid.NewGuid();
        var added = await sec.PostAsJsonAsync($"/api/meetings/{meeting.Id}/agenda/items", new
        {
            topicId,
            topicKey = "TOP-2026-022",
            topicTitle = "Adopt an event-driven integration layer",
            urgent = false,
            timeboxMinutes = 15,
            presenterUserId = (Guid?)null,
            presenterName = (string?)null,
        });
        added.StatusCode.Should().Be(HttpStatusCode.OK);

        return (meeting.Id, meeting.Key, topicId);
    }

    private static object InviteBody(Guid topicId, string email = "guest@vendor.example") =>
        new { topicId, email, fullName = "Nadia Presenter" };

    [Fact] // AC-008
    public async Task Inviting_a_guest_presenter_without_a_token_is_401()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var (meetingId, _, topicId) = await ScheduleWithOneItemAsync(factory, FutureEnd);

        var response = await Client(factory, roles: null)
            .PostAsJsonAsync($"/api/meetings/{meetingId}/guest-presenters", InviteBody(topicId));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory] // FR-159 — SECRETARY only, narrower than the Chairman+Secretary pair that owns the agenda
    [InlineData("Chairman")]
    [InlineData("Administrator")]
    [InlineData("Member")]
    [InlineData("Reviewer")]
    [InlineData("Auditor")]
    public async Task Inviting_a_guest_presenter_is_403_for_every_role_except_Secretary(string role)
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var (meetingId, _, topicId) = await ScheduleWithOneItemAsync(factory, FutureEnd);

        var response = await Client(factory, role, sub: $"kc-{role}")
            .PostAsJsonAsync($"/api/meetings/{meetingId}/guest-presenters", InviteBody(topicId));

        // Chairman is refused DELIBERATELY. Seniority is not the axis: FR-159 gives this to the role
        // that schedules the meeting and knows who presents, and it hands an external person real
        // read access — so the capability is narrower than Agenda.Publish, not equal to it.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // And nothing was created on the way to being refused.
        factory.Identity.Created.Should().BeEmpty();
    }

    [Fact] // AC-092 — the writer: the window is the meeting's end plus the approved grace (DEC-040)
    public async Task Inviting_a_guest_presenter_stores_the_meeting_end_plus_the_grace_as_the_access_window()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var (meetingId, key, topicId) = await ScheduleWithOneItemAsync(factory, FutureEnd);
        var sec = Client(factory, "Secretary", sub: "kc-sec");

        var response = await sec.PostAsJsonAsync($"/api/meetings/{meetingId}/guest-presenters", InviteBody(topicId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var invited = (await response.Content.ReadFromJsonAsync<InvitedGuest>())!;
        invited.AccessExpiresAt.Should().Be(FutureEnd + GuestAccess.Grace);
        invited.TemporaryPassword.Should().NotBeNullOrWhiteSpace();

        // THE STORED COLUMN, not the response echo: this is the single value the refusal, the sweep
        // and the /session banner all read, and DEC-037 requires that they cannot disagree.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MembershipDbContext>();
        var member = await db.Members.FirstAsync(m => m.PublicId == invited.PublicId);
        member.AccessExpiresAt.Should().Be(FutureEnd + GuestAccess.Grace);
        member.Role.Should().Be(Modules.Membership.Domain.Enums.CommitteeRole.Guest);
        member.Status.Should().Be(Modules.Membership.Domain.Enums.MembershipStatus.Invited);

        // The identity provider was actually asked to create the account — the local row alone would
        // be a person who can never sign in, and DEF-029 means that row could never be deleted.
        factory.Identity.Created.Should().ContainSingle().Which.Email.Should().Be("guest@vendor.example");

        // And the guest holds the slot, which is what makes /session able to show them anything.
        var detail = await (await sec.GetAsync($"/api/meetings/{key}")).Content.ReadFromJsonAsync<MeetingDetail>();
        var item = detail!.Agenda!.Items.Single(i => i.TopicId == topicId);
        item.PresenterUserId.Should().Be(invited.PublicId);
        item.PresenterName.Should().Be("Nadia Presenter");
    }

    [Fact] // AC-093 — the governance record, asserted as ROWS rather than as an emission
    public async Task Inviting_a_guest_presenter_writes_both_audit_rows()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var (meetingId, _, topicId) = await ScheduleWithOneItemAsync(factory, FutureEnd);

        await Client(factory, "Secretary", sub: "kc-sec")
            .PostAsJsonAsync($"/api/meetings/{meetingId}/guest-presenters", InviteBody(topicId));

        using var scope = factory.Services.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var actions = await audit.AuditEvents.Select(e => e.Action).ToListAsync();

        // Two rows saying different things: an account was created, AND an outsider was given a slot
        // in this meeting. Either one alone leaves the record unable to answer a real question.
        actions.Should().Contain("Membership.GuestPresenterInvited");
        actions.Should().Contain("Meetings.GuestPresenterInvited");
    }

    [Fact] // the irreversible half must not run for a slot that does not exist
    public async Task A_topic_that_is_not_on_the_agenda_creates_no_account_at_all()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var (meetingId, _, _) = await ScheduleWithOneItemAsync(factory, FutureEnd);

        var response = await Client(factory, "Secretary", sub: "kc-sec")
            .PostAsJsonAsync($"/api/meetings/{meetingId}/guest-presenters", InviteBody(Guid.NewGuid()));

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
        // The assertion that matters: the Keycloak account and the member row are the parts that
        // cannot be undone (DEF-029), so a bad slot has to fail BEFORE either exists.
        factory.Identity.Created.Should().BeEmpty();
    }

    [Fact] // an unknown meeting must fail before anything is created, for the same reason
    public async Task An_unknown_meeting_creates_no_account_at_all()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        await ScheduleWithOneItemAsync(factory, FutureEnd);

        var response = await Client(factory, "Secretary", sub: "kc-sec")
            .PostAsJsonAsync($"/api/meetings/{Guid.NewGuid()}/guest-presenters", InviteBody(Guid.NewGuid()));

        response.StatusCode.Should().NotBe(HttpStatusCode.OK);
        factory.Identity.Created.Should().BeEmpty();
    }

    [Fact] // the duplicate guard runs BEFORE Keycloak, so a repeat invite leaves no stray account
    public async Task Inviting_the_same_email_twice_is_refused_without_creating_a_second_account()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var (meetingId, _, topicId) = await ScheduleWithOneItemAsync(factory, FutureEnd);
        var sec = Client(factory, "Secretary", sub: "kc-sec");

        var first = await sec.PostAsJsonAsync($"/api/meetings/{meetingId}/guest-presenters", InviteBody(topicId));
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await sec.PostAsJsonAsync($"/api/meetings/{meetingId}/guest-presenters", InviteBody(topicId));

        second.StatusCode.Should().NotBe(HttpStatusCode.OK);
        // ONE account, not two: the check has to happen before the identity provider is touched, or a
        // refused request still leaves a real user behind in Keycloak.
        factory.Identity.Created.Should().ContainSingle();
    }

    [Fact] // AC-092 — FORCED REFUSAL. The window is real, and the invite alone is what closes it.
    public async Task A_guest_invited_for_a_meeting_that_has_already_passed_is_refused_on_their_next_request()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var (meetingId, _, topicId) = await ScheduleWithOneItemAsync(factory, PastEnd);

        var invited = await (await Client(factory, "Secretary", sub: "kc-sec")
            .PostAsJsonAsync($"/api/meetings/{meetingId}/guest-presenters", InviteBody(topicId)))
            .Content.ReadFromJsonAsync<InvitedGuest>();

        // Nothing is poked afterwards: the meeting's own end date is what puts this guest past their
        // window, so this proves the WRITER, not just the enforcement that was already shipped.
        var guest = Client(factory, "Guest", sub: $"kc-{invited!.Email}");
        var response = await guest.PostAsync("/api/members/me", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.GetValues("X-Acmp-Auth-Reason").Should().ContainSingle().Which.Should().Be("access_expired");
    }

    [Fact] // the other half of the boundary — an open window is NOT a blanket ban
    public async Task A_guest_invited_for_an_upcoming_meeting_is_admitted()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var (meetingId, _, topicId) = await ScheduleWithOneItemAsync(factory, FutureEnd);

        var invited = await (await Client(factory, "Secretary", sub: "kc-sec")
            .PostAsJsonAsync($"/api/meetings/{meetingId}/guest-presenters", InviteBody(topicId)))
            .Content.ReadFromJsonAsync<InvitedGuest>();

        var response = await Client(factory, "Guest", sub: $"kc-{invited!.Email}")
            .PostAsync("/api/members/me", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
