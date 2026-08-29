using System.Net;
using System.Net.Http.Json;
using Acmp.Modules.Meetings.Application.Features.InviteGuestPresenter;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Acmp.Api.Tests;

// FR-159 / AC-092 / DEC-037 — /session, the guest presenter's surface.
//
// The guest under test is created by the REAL invite from PR1 rather than seeded, so these also prove
// the two halves fit: the person the Secretary invited is the person /session answers for, found
// through the slot the invite assigned. Only the topic is seeded, because the invite does not create
// topics and uploading a real file would need an object store.
public class SessionApiTests
{
    private static readonly DateTimeOffset FutureEnd = DateTimeOffset.Parse("2099-07-01T10:30:00Z");

    private sealed class FakeFileStore : IFileStore
    {
        public Task<string> UploadAsync(string bucket, string objectName, Stream content, string contentType, CancellationToken ct = default)
            => Task.FromResult($"{bucket}/{objectName}");
        public Task<string> GetPreSignedUrlAsync(string bucket, string objectName, TimeSpan expiry, CancellationToken ct = default)
            => Task.FromResult($"https://minio.test/{bucket}/{objectName}");
        public Task<bool> ExistsAsync(string bucket, string objectName, CancellationToken ct = default) => Task.FromResult(true);
        public Task DeleteAsync(string bucket, string objectName, CancellationToken ct = default) => Task.CompletedTask;
    }

    // Shares the base factory's in-memory databases (the db names are that instance's), so a guest
    // invited through the plain client is visible to a client built from this one.
    private static WebApplicationFactory<Program> WithFakeStore(AcmpWebApplicationFactory factory) =>
        factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IFileStore>();
            s.AddSingleton<IFileStore>(new FakeFileStore());
        }));

    private static HttpClient Client(WebApplicationFactory<Program> factory, string? roles, string sub = "u1")
    {
        var client = factory.CreateClient();
        if (roles is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
            client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        }
        return client;
    }

    private sealed record MeetingSummary(Guid Id, string Key);
    private sealed record ProvisionedMember(Guid PublicId);
    private sealed record InvitedGuest(Guid PublicId, string Email, DateTimeOffset AccessExpiresAt);
    private sealed record Material(Guid Id, string FileName, string ContentType, long SizeBytes);
    private sealed record Session(
        DateTimeOffset? AccessExpiresAt, string MeetingKey, string MeetingTitle,
        DateTimeOffset SlotStart, DateTimeOffset SlotEnd, int ItemNumber, int ItemCount, int TimeboxMinutes,
        string TopicKey, string TopicTitle, string TopicSummary, List<Material> Materials);
    private sealed record MaterialUrl(string Url);

    /// <summary>Seeds a topic with two attachments and returns its id.</summary>
    private static async Task<(Guid TopicId, Guid FirstAttachmentId)> SeedTopicAsync(
        AcmpWebApplicationFactory factory, string key = "TOP-2026-022")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TopicsDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var topic = Topic.Draft(key, "Standardize API pagination across public services",
            "A proposal to mandate cursor-based pagination for all public-facing APIs.", "Inconsistent offsets are risky.",
            TopicType.ArchitectureDecision, TopicUrgency.Normal, TopicSource.CommitteeMember, "kc-sec", "Secretary",
            new[] { "platform" }, Array.Empty<string>(), Array.Empty<string>());
        var deck = topic.AddAttachment("proposal.pdf", "application/pdf", 2048, "key-1", "kc-sec", "Secretary", clock.UtcNow);
        topic.AddAttachment("sequence.svg", "image/svg+xml", 512, "key-2", "kc-sec", "Secretary", clock.UtcNow);
        db.Topics.Add(topic);
        await db.SaveChangesAsync();

        return (topic.PublicId, deck.PublicId);
    }

    /// <summary>Schedules a meeting, places the seeded topic on it, and invites a guest to present it.</summary>
    private static async Task<(InvitedGuest Guest, string MeetingKey)> InviteGuestForAsync(
        AcmpWebApplicationFactory factory, Guid topicId, int timebox = 15, int leadingItems = 0)
    {
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

        // Items placed BEFORE the guest's, so "Item N of M" and the planned start are not trivially 1 and 0.
        for (var i = 0; i < leadingItems; i++)
        {
            await sec.PostAsJsonAsync($"/api/meetings/{meeting.Id}/agenda/items", new
            {
                topicId = Guid.NewGuid(),
                topicKey = $"TOP-2026-00{i}",
                topicTitle = $"Earlier item {i}",
                urgent = false,
                timeboxMinutes = 20,
                presenterUserId = (Guid?)null,
                presenterName = (string?)null,
            });
        }

        await sec.PostAsJsonAsync($"/api/meetings/{meeting.Id}/agenda/items", new
        {
            topicId,
            topicKey = "TOP-2026-022",
            topicTitle = "Standardize API pagination across public services",
            urgent = false,
            timeboxMinutes = timebox,
            presenterUserId = (Guid?)null,
            presenterName = (string?)null,
        });

        var invited = await (await sec.PostAsJsonAsync($"/api/meetings/{meeting.Id}/guest-presenters",
            new { topicId, email = "nadia@vendor.example", fullName = "Nadia Presenter" }))
            .Content.ReadFromJsonAsync<InvitedGuest>();

        return (invited!, meeting.Key);
    }

    [Fact] // AC-008
    public async Task Session_without_a_token_is_401()
    {
        await using var factory = new AcmpWebApplicationFactory();

        var response = await Client(factory, roles: null).GetAsync("/api/session/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory] // DEC-037 — Guest plus Chairman/Secretary, enforced at the API and not only by the route guard
    [InlineData("Member")]
    [InlineData("Reviewer")]
    [InlineData("Auditor")]
    [InlineData("Administrator")]
    [InlineData("Submitter")]
    public async Task Session_is_403_for_a_role_outside_the_guest_surface(string role)
    {
        await using var factory = new AcmpWebApplicationFactory();

        var response = await Client(factory, role, sub: $"kc-{role}").GetAsync("/api/session/me");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // "you are not presenting" is a state, not a missing resource
    public async Task A_caller_with_no_slot_gets_no_content()
    {
        await using var factory = new AcmpWebApplicationFactory();

        var response = await Client(factory, "Secretary", sub: "kc-sec").GetAsync("/api/session/me");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact] // AC-092 / DEC-037 — the whole GUEST/PRESENTER SHELL, from the person the Secretary invited
    public async Task A_guest_sees_their_own_slot_the_topic_card_and_its_materials()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var (topicId, _) = await SeedTopicAsync(factory);
        var (guest, meetingKey) = await InviteGuestForAsync(factory, topicId, timebox: 15, leadingItems: 2);

        var response = await Client(factory, "Guest", sub: $"kc-{guest.Email}").GetAsync("/api/session/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = (await response.Content.ReadFromJsonAsync<Session>())!;

        // THE BANNER READS THE SAME STORED VALUE THE SERVER ENFORCES — the point of AC-092's second half.
        session.AccessExpiresAt.Should().Be(guest.AccessExpiresAt);
        session.AccessExpiresAt.Should().Be(FutureEnd + GuestAccess.Grace);

        session.MeetingKey.Should().Be(meetingKey);
        session.MeetingTitle.Should().Be("Weekly Architecture Committee");

        // "Item 3 of 6" in the design: third, after the two 20-minute items placed before it.
        session.ItemNumber.Should().Be(3);
        session.ItemCount.Should().Be(3);
        session.TimeboxMinutes.Should().Be(15);
        session.SlotStart.Should().Be(FutureEnd.AddHours(-2).AddMinutes(40));
        session.SlotEnd.Should().Be(session.SlotStart.AddMinutes(15));

        session.TopicKey.Should().Be("TOP-2026-022");
        session.TopicTitle.Should().Be("Standardize API pagination across public services");
        session.TopicSummary.Should().StartWith("A proposal to mandate cursor-based pagination");
        session.Materials.Select(m => m.FileName).Should().ContainInOrder("proposal.pdf", "sequence.svg");
    }

    [Fact] // FR-159 / NFR-027 — materials open by short-lived pre-signed URL
    public async Task A_guest_can_open_a_material_on_their_own_slot()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var (topicId, deckId) = await SeedTopicAsync(factory);
        var (guest, _) = await InviteGuestForAsync(factory, topicId);

        var response = await Client(WithFakeStore(factory), "Guest", sub: $"kc-{guest.Email}")
            .GetAsync($"/api/session/materials/{deckId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<MaterialUrl>();
        body!.Url.Should().StartWith("https://minio.test/");
    }

    [Fact] // the guarantee that matters: one slot does not open another topic's files
    public async Task A_guest_cannot_open_a_material_belonging_to_another_topic()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var (mineId, _) = await SeedTopicAsync(factory);
        var (_, theirDeckId) = await SeedTopicAsync(factory, key: "TOP-2026-099");
        var (guest, _) = await InviteGuestForAsync(factory, mineId);

        var response = await Client(WithFakeStore(factory), "Guest", sub: $"kc-{guest.Email}")
            .GetAsync($"/api/session/materials/{theirDeckId}");

        // 404 and not 403: the same answer an unknown id gets, so the response cannot be used to
        // discover which attachment ids exist.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_unknown_material_is_not_found()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var (topicId, _) = await SeedTopicAsync(factory);
        var (guest, _) = await InviteGuestForAsync(factory, topicId);

        var response = await Client(WithFakeStore(factory), "Guest", sub: $"kc-{guest.Email}")
            .GetAsync($"/api/session/materials/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── the three guards the coverage gate found untested (FR-165's refactor) ──────────────────────
    //
    // ⚠⚠ THESE WERE NEVER COVERED, AND THE REFACTOR IS WHAT MADE THAT VISIBLE RATHER THAN WHAT CAUSED IT.
    // Extracting the shell into PresenterSessionComposer removed ~36 lines of COVERED composition from
    // this file. The same three early returns stayed untested, and their share of a smaller file crossed
    // ADR-0016's 5% budget. So a refactor can push a file under a per-file floor WITHOUT INTRODUCING A
    // SINGLE NEW UNTESTED LINE — the numerator never moved, the denominator did. Worth knowing before
    // reading such a failure as "the new code is untested".

    // A Chairman with a member row and no agenda slot anywhere. Distinct from the no-member-row case
    // above it, which returns earlier and is what the existing "no slot" test actually exercised.
    [Fact]
    public async Task A_caller_who_is_provisioned_but_presents_nothing_gets_no_content()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var chair = Client(factory, "Chairman", sub: "kc-chair");
        await chair.PostAsync("/api/members/me", null); // provision, so the directory resolves them

        var response = await chair.GetAsync("/api/session/me");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // Presenting at a CANCELLED meeting: the slot exists, the meeting is filtered out, and the page must
    // say "you are not presenting" rather than send someone to a meeting that will not happen.
    //
    // ⚠⚠ THE PRESENTER HERE IS A CHAIRMAN, NOT A GUEST, AND THE REASON IS A BEHAVIOUR WORTH KNOWING.
    // A guest cannot reach this guard at all: CancelMeeting calls GuestWindows.CloseOrphanedAsync, so
    // cancelling CLOSES the window of any guest not presenting elsewhere, and their next request is a
    // 401 access_expired long before the "which meeting" filter runs. The first draft of this test used
    // a guest and got exactly that 401 — correct product behaviour, wrong assertion. So the meeting-
    // filter guard is reachable only by a principal whose access does not expire.
    [Fact]
    public async Task A_presenter_whose_only_meeting_was_cancelled_gets_no_content()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();
        var (topicId, _) = await SeedTopicAsync(factory, key: "TOP-2026-055");

        var chair = Client(factory, "Chairman", sub: "kc-chair-cancelled");
        var me = await (await chair.PostAsync("/api/members/me", null)).Content.ReadFromJsonAsync<ProvisionedMember>();

        var sec = Client(factory, "Secretary", sub: "kc-sec");
        var scheduled = await sec.PostAsJsonAsync("/api/meetings", new
        {
            title = "Meeting that gets cancelled",
            chairUserId = me!.PublicId,
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
            topicKey = "TOP-2026-055",
            topicTitle = "Cancelled-meeting slot",
            urgent = false,
            timeboxMinutes = 15,
            presenterUserId = me.PublicId,
            presenterName = "Sara Chair",
        });

        var cancelled = await sec.PostAsJsonAsync($"/api/meetings/{meeting.Id}/cancel",
            new { reason = "Quorum could not be reached" });
        cancelled.IsSuccessStatusCode.Should().BeTrue("the cancellation is this test's precondition");

        var response = await chair.GetAsync("/api/session/me");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // The material handler's own member-row guard. Fails closed as 404 — the same answer as an attachment
    // that does not exist, so the response cannot be used to probe for one.
    [Fact]
    public async Task A_material_request_from_a_caller_with_no_member_row_is_not_found()
    {
        await using var factory = AcmpWebApplicationFactory.WithIdentityProvider();

        var response = await Client(WithFakeStore(factory), "Chairman", sub: "kc-never-provisioned")
            .GetAsync($"/api/session/materials/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
