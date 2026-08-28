using System.Net;
using System.Net.Http.Json;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Audit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Api.Tests;

// FR-165 / DEC-086 — the Chairman/Secretary preview of a CHOSEN presenter's /session view.
//
// TREAT THE REFUSAL AS THE FEATURE AND PROVE IT BY FORCING IT (DW-028's own instruction). This surface
// adds a targeting parameter to a read path whose security has until now been the ABSENCE of one, so
// every refusal below is forced through the real HTTP pipeline rather than reasoned about.
//
// ⚠⚠ THE THREE LAYERS ARE ASSERTED SEPARATELY, BY SIGNATURE, AND THAT IS THE DESIGN OF THIS FILE. Three
// tests that all assert "403" would look like defence in depth while actually testing whichever layer
// happens to run first — and if that layer were ever removed the other two assertions would keep passing
// on its replacement. So:
//   - a GUEST is refused by GuestSurfaceMiddleware AT THE PATH, identified by the X-Acmp-Auth-Reason
//     header it alone sets. That refusal happens before any handler runs and without a database.
//   - a MEMBER, REVIEWER, AUDITOR, ADMINISTRATOR or SUBMITTER is not guest-only, so the path gate passes
//     them and AllowedRoles refuses them at the application boundary — identified by the ABSENCE of that
//     header plus the presence of an Authorization.Forbidden audit row.
// The two are genuinely different mechanisms answering for different principals, and neither covers the
// other's population.
public class SessionPreviewApiTests
{
    private static readonly DateTimeOffset FutureEnd = DateTimeOffset.Parse("2099-08-01T10:30:00Z");
    private const string ReasonHeader = "X-Acmp-Auth-Reason";

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

    // `Action ?? EventType` — the store holds two row shapes and IAuditSink.EmitAsync writes the LEAN one
    // with Action NULL, which is the path both Authorization.Forbidden and Session.PresenterPreviewed take.
    // Selecting Action alone returns null for exactly the rows these tests exist to find; RefusalAuditTests
    // records that this is not hypothetical, and a collection of nulls satisfies a NotContain vacuously.
    private static async Task<IReadOnlyList<string>> AuditActionsAsync(AcmpWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        return await audit.AuditEvents.Select(e => e.Action ?? e.EventType).ToListAsync();
    }

    private static async Task<Guid> SeedTopicAsync(AcmpWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TopicsDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var topic = Topic.Draft("TOP-2026-044", "Adopt a shared retry policy for outbound calls",
            "A proposal to standardise retry and backoff across integrations.", "Ad-hoc retries are unsafe.",
            TopicType.ArchitectureDecision, TopicUrgency.Normal, TopicSource.CommitteeMember, "kc-sec", "Secretary",
            new[] { "platform" }, Array.Empty<string>(), Array.Empty<string>());
        topic.AddAttachment("deck.pdf", "application/pdf", 4096, "key-a", "kc-sec", "Secretary", clock.UtcNow);
        db.Topics.Add(topic);
        await db.SaveChangesAsync();
        return topic.PublicId;
    }

    private sealed record MeetingSummary(Guid Id, string Key);
    private sealed record InvitedGuest(Guid PublicId, string Email, DateTimeOffset AccessExpiresAt);
    private sealed record Session(
        DateTimeOffset? AccessExpiresAt, string MeetingKey, string MeetingTitle,
        DateTimeOffset SlotStart, DateTimeOffset SlotEnd, int ItemNumber, int ItemCount, int TimeboxMinutes,
        string TopicKey, string TopicTitle, string TopicSummary, List<object> Materials);

    /// <summary>Schedules a meeting with the topic on the agenda; invites a guest to present it unless told not to.</summary>
    private static async Task<(Guid MeetingId, Guid TopicId, InvitedGuest? Guest)> ScenarioAsync(
        AcmpWebApplicationFactory factory, bool withPresenter = true)
    {
        var topicId = await SeedTopicAsync(factory);
        var sec = Client(factory, "Secretary", sub: "kc-sec");

        var scheduled = await sec.PostAsJsonAsync("/api/meetings", new
        {
            title = "Weekly Architecture Committee",
            chairUserId = Guid.NewGuid(),
            chairName = "Sara Chair",
            scheduledStart = FutureEnd.AddHours(-2),
            scheduledEnd = FutureEnd,
            location = (string?)null,
            joinUrl = (string?)null,
        });
        var meeting = (await scheduled.Content.ReadFromJsonAsync<MeetingSummary>())!;

        await sec.PostAsJsonAsync($"/api/meetings/{meeting.Id}/agenda/items", new
        {
            topicId,
            topicKey = "TOP-2026-044",
            topicTitle = "Adopt a shared retry policy for outbound calls",
            urgent = false,
            timeboxMinutes = 15,
            presenterUserId = (Guid?)null,
            presenterName = (string?)null,
        });

        if (!withPresenter)
            return (meeting.Id, topicId, null);

        var invite = await sec.PostAsJsonAsync($"/api/meetings/{meeting.Id}/guest-presenters",
            new { topicId, email = "omar@vendor.example", fullName = "Omar Presenter" });

        // ⚠ THE SETUP ASSERTS ITSELF, and it does so because it already failed silently once. The factory
        // registers no IIdentityProvider unless asked (ADR-0040 / DEF-029), so without
        // WithIdentityProvider() this POST returns an error — and ReadFromJsonAsync happily deserialises
        // that body into an InvitedGuest with default fields. A NotBeNull() check on the result therefore
        // PASSED while no guest existed, the agenda item kept a null presenter, and the preview correctly
        // answered 204 — which reads exactly like the feature being broken. A scenario that cannot build
        // its own preconditions must fail HERE, loudly, not hand a hollow 204 to an assertion downstream.
        invite.IsSuccessStatusCode.Should().BeTrue("the guest invite is this scenario's precondition");
        var invited = (await invite.Content.ReadFromJsonAsync<InvitedGuest>())!;
        invited.PublicId.Should().NotBe(Guid.Empty, "a defaulted record is what a failed invite deserialises to");

        return (meeting.Id, topicId, invited);
    }

    private static string Url(Guid meetingId, Guid topicId) =>
        $"/api/session-preview?meetingId={meetingId}&topicId={topicId}";

    [Fact]
    public async Task Preview_without_a_token_is_401()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();

        var response = await Client(factory, roles: null).GetAsync(Url(Guid.NewGuid(), Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // LAYER 2 — the path gate. THE HEADER IS THE ASSERTION, not the status code: a bare 403 here would be
    // satisfied by the application-layer refusal too, so it could not tell "the guest never reached a
    // handler" from "a handler turned them away". GuestSurfaceMiddleware is the only thing that sets this
    // reason, and it can only have run if /api/session-preview is genuinely OUTSIDE the /api/session
    // allowlist — which is the whole reason the endpoint has its own group (DEC-086 d1).
    //
    // ⚠ THE MUTATION THIS KILLS is moving the endpoint under /api/session: the segment prefix would then
    // match, the guest would sail through the gate, and this header would disappear while a naive
    // status-only test stayed green on the layer below.
    [Fact]
    public async Task A_guest_is_refused_at_the_PATH_before_any_handler_runs()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var (meetingId, topicId, guest) = await ScenarioAsync(factory);

        // The guest is the person this slot BELONGS to, which is the strongest form of the test: even the
        // presenter themselves may not use the targeting parameter, because the parameter is not theirs.
        var response = await Client(factory, "Guest", sub: "kc-guest").GetAsync(Url(meetingId, topicId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Headers.TryGetValues(ReasonHeader, out var reason).Should().BeTrue(
            "the path gate refused this, not the handler — and the two must stay distinguishable");
        reason!.Should().Contain("guest_scope");
        guest.Should().NotBeNull();
    }

    // LAYER 3 — the application boundary. These roles are NOT guest-only, so the path gate passes them and
    // AllowedRoles is the only thing standing between them and another person's slot. Asserted by the
    // ABSENCE of the path gate's header plus the PRESENCE of the audit row, so this cannot silently become
    // a second test of layer 2.
    [Theory]
    [InlineData("Member")]
    [InlineData("Reviewer")]
    [InlineData("Auditor")]
    [InlineData("Administrator")]
    [InlineData("Submitter")]
    public async Task A_role_outside_Chairman_and_Secretary_is_refused_at_the_application_boundary(string role)
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var (meetingId, topicId, _) = await ScenarioAsync(factory);

        var response = await Client(factory, role, sub: $"kc-{role}").GetAsync(Url(meetingId, topicId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Headers.Contains(ReasonHeader).Should().BeFalse(
            "this principal is not guest-only, so the path gate passed them and AllowedRoles is what refused");
        (await AuditActionsAsync(factory)).Should().Contain("Authorization.Forbidden",
            "a refusal nobody audits is DEF-056");
    }

    // THE POSITIVE CASE, and the assertion that makes it a PREVIEW rather than a rename of /session: the
    // Secretary's OWN session is empty (they present nothing), while the preview returns a full slot. If
    // the targeting parameter were ignored — the most likely way this feature silently does nothing — the
    // preview would answer 204 exactly like the caller-scoped read, and a test that only checked for 200
    // on some payload would not notice.
    [Fact]
    public async Task A_secretary_sees_the_TARGETED_presenters_slot_and_that_persons_expiry()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var (meetingId, topicId, guest) = await ScenarioAsync(factory);
        var sec = Client(factory, "Secretary", sub: "kc-sec");

        var own = await sec.GetAsync("/api/session/me");
        own.StatusCode.Should().Be(HttpStatusCode.NoContent, "the Secretary is not presenting anything");

        var response = await sec.GetAsync(Url(meetingId, topicId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = (await response.Content.ReadFromJsonAsync<Session>())!;
        session.TopicKey.Should().Be("TOP-2026-044");
        session.ItemNumber.Should().Be(1);
        session.TimeboxMinutes.Should().Be(15);
        // THE BANNER IS THE TARGET'S, NOT THE CALLER'S. A Chairman or Secretary's own access never
        // expires, so composing this from the caller's row would render a banner no presenter will ever
        // see — and it would look perfectly correct on screen.
        session.AccessExpiresAt.Should().Be(guest!.AccessExpiresAt);
        session.Materials.Should().HaveCount(1, "materials are LISTED in the preview (DEC-086 d2)");
    }

    // DEC-086 d3 — the successful preview is recorded, and this is only the second place in the product
    // where a successful READ is audited. Asserted as a ROW, never as an emission.
    [Fact]
    public async Task A_successful_preview_leaves_a_Session_PresenterPreviewed_row()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var (meetingId, topicId, _) = await ScenarioAsync(factory);

        var before = await AuditActionsAsync(factory);
        before.Should().NotContain("Session.PresenterPreviewed",
            "the control: without it, a row present for any other reason would satisfy the assertion below");

        var response = await Client(factory, "Chairman", sub: "kc-chair").GetAsync(Url(meetingId, topicId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await AuditActionsAsync(factory)).Should().Contain("Session.PresenterPreviewed");
    }

    // EMPTY-STATE PARITY (FR-165): a slot nobody is presenting is what the PRESENTER's page would show as
    // "you are not presenting", so the preview shows it too. And no audit row, because nothing was read —
    // the boundary DEC-086 d3 draws is disclosure, not the attempt.
    [Fact]
    public async Task A_slot_with_no_presenter_assigned_is_204_and_audits_nothing()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var (meetingId, topicId, _) = await ScenarioAsync(factory, withPresenter: false);

        var response = await Client(factory, "Secretary", sub: "kc-sec").GetAsync(Url(meetingId, topicId));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AuditActionsAsync(factory)).Should().NotContain("Session.PresenterPreviewed",
            "nothing was disclosed, so there is nothing to record");
    }
}
