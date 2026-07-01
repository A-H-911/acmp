using System.Net;
using System.Net.Http.Json;
using Acmp.Modules.Meetings.Domain;
using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Api.Tests;

// HTTP-contract tests for /api/minutes through the real pipeline + policy authorization (docs/10). Draft
// needs a held meeting (seeded directly), since the MoM references a real meeting in its own module.
// Draft/submit = Minutes.Capture; approve/publish/supersede = Minutes.Approve — both Chairman/Secretary.
public class MinutesApiTests
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

    private static object Loc(string en, string ar) => new { en, ar };
    private static object Summary => Loc("Roadmap discussed", "نوقشت خارطة الطريق");

    private static async Task<Guid> SeedHeldMeetingAsync(AcmpWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        var m = Meeting.Schedule("MTG-2026-001", "Weekly Committee", Meeting.SingleCommitteeId, Guid.NewGuid(), "Chair",
            DateTimeOffset.Parse("2026-07-01T09:00:00Z"), DateTimeOffset.Parse("2026-07-01T10:30:00Z"),
            MeetingType.Regular, MeetingMode.InPerson, null, null, DateTimeOffset.UtcNow);
        m.Start(DateTimeOffset.UtcNow);
        m.Hold(DateTimeOffset.UtcNow);
        db.Meetings.Add(m);
        await db.SaveChangesAsync();
        return m.PublicId;
    }

    private sealed record MinutesSummary(Guid Id, string Key, int Version, string Status);
    private sealed record MinutesDetail(string Key, int Version, string Status, bool ApprovedBySoleAuthor, Guid? SupersededByMinutesId);

    [Fact] // AC-008
    public async Task Draft_without_token_returns_401()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, roles: null)
            .PostAsJsonAsync("/api/minutes", new { meetingId = Guid.NewGuid(), summary = Summary });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // docs/10: Minutes.Capture is Secretary/Chairman — a Member is forbidden
    public async Task Member_cannot_draft_minutes_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, "Member")
            .PostAsJsonAsync("/api/minutes", new { meetingId = Guid.NewGuid(), summary = Summary });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Draft_with_empty_summary_returns_400()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var meetingId = await SeedHeldMeetingAsync(factory);
        var response = await Client(factory, "Secretary", "kc-sec")
            .PostAsJsonAsync("/api/minutes", new { meetingId, summary = Loc("", "") });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Draft_for_an_unknown_meeting_returns_404()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var response = await Client(factory, "Secretary", "kc-sec")
            .PostAsJsonAsync("/api/minutes", new { meetingId = Guid.NewGuid(), summary = Summary });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact] // W10: draft → detail → version list; unknown key 404
    public async Task Secretary_drafts_then_reads_detail_and_list()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var meetingId = await SeedHeldMeetingAsync(factory);
        var sec = Client(factory, "Secretary", "kc-sec");

        var draft = await sec.PostAsJsonAsync("/api/minutes", new { meetingId, summary = Summary });
        draft.StatusCode.Should().Be(HttpStatusCode.Created);
        var minutes = await draft.Content.ReadFromJsonAsync<MinutesSummary>();
        minutes!.Key.Should().Be("MIN-2026-001");
        minutes.Status.Should().Be("Draft");

        var detail = await sec.GetAsync($"/api/minutes/{minutes.Key}");
        (await detail.Content.ReadFromJsonAsync<MinutesDetail>())!.Status.Should().Be("Draft");

        var list = await (await sec.GetAsync($"/api/minutes?meeting={meetingId}")).Content.ReadFromJsonAsync<List<MinutesSummary>>();
        list!.Should().ContainSingle().Which.Key.Should().Be("MIN-2026-001");

        (await sec.GetAsync("/api/minutes/MIN-2026-999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact] // W10 full path: draft → submit → approve → publish (AC-038). Same actor → sole-author flagged (AC-014).
    public async Task Secretary_drafts_submits_approves_and_publishes()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var meetingId = await SeedHeldMeetingAsync(factory);
        var sec = Client(factory, "Secretary", "kc-sec");

        var minutes = await (await sec.PostAsJsonAsync("/api/minutes", new { meetingId, summary = Summary })).Content.ReadFromJsonAsync<MinutesSummary>();
        (await sec.PostAsync($"/api/minutes/{minutes!.Id}/submit", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await sec.PostAsync($"/api/minutes/{minutes.Id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await sec.PostAsync($"/api/minutes/{minutes.Id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await (await sec.GetAsync($"/api/minutes/{minutes.Key}")).Content.ReadFromJsonAsync<MinutesDetail>();
        detail!.Status.Should().Be("Published");
        detail.ApprovedBySoleAuthor.Should().BeTrue(); // author == approver
    }

    [Fact] // AC-037: request-changes bounces InReview → Draft
    public async Task Chairman_requests_changes_returns_to_draft()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var meetingId = await SeedHeldMeetingAsync(factory);
        var sec = Client(factory, "Secretary", "kc-sec");
        var minutes = await (await sec.PostAsJsonAsync("/api/minutes", new { meetingId, summary = Summary })).Content.ReadFromJsonAsync<MinutesSummary>();
        await sec.PostAsync($"/api/minutes/{minutes!.Id}/submit", null);

        var chair = Client(factory, "Chairman", "kc-chair");
        (await chair.PostAsync($"/api/minutes/{minutes.Id}/request-changes", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = await (await sec.GetAsync($"/api/minutes/{minutes.Key}")).Content.ReadFromJsonAsync<MinutesDetail>();
        detail!.Status.Should().Be("Draft");
    }

    [Fact] // docs/10: Minutes.Approve is Chairman/Secretary — a Member cannot approve
    public async Task Member_cannot_approve_minutes_403()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var meetingId = await SeedHeldMeetingAsync(factory);
        var sec = Client(factory, "Secretary", "kc-sec");
        var minutes = await (await sec.PostAsJsonAsync("/api/minutes", new { meetingId, summary = Summary })).Content.ReadFromJsonAsync<MinutesSummary>();
        await sec.PostAsync($"/api/minutes/{minutes!.Id}/submit", null);

        (await Client(factory, "Member").PostAsync($"/api/minutes/{minutes.Id}/approve", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact] // AC-036: supersede a published MoM — successor 201 v2 Published, prior flips to Superseded with a back-link
    public async Task Secretary_supersedes_a_published_mom()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var meetingId = await SeedHeldMeetingAsync(factory);
        var sec = Client(factory, "Secretary", "kc-sec");
        var minutes = await (await sec.PostAsJsonAsync("/api/minutes", new { meetingId, summary = Summary })).Content.ReadFromJsonAsync<MinutesSummary>();
        await sec.PostAsync($"/api/minutes/{minutes!.Id}/submit", null);
        await sec.PostAsync($"/api/minutes/{minutes.Id}/approve", null);
        await sec.PostAsync($"/api/minutes/{minutes.Id}/publish", null);

        var supersede = await sec.PostAsJsonAsync($"/api/minutes/{minutes.Id}/supersede", new
        {
            summary = Loc("Corrected minutes", "محضر مصحح"),
            reason = Loc("Fixed attendance", "تصحيح الحضور"),
        });
        supersede.StatusCode.Should().Be(HttpStatusCode.Created);
        var successor = await supersede.Content.ReadFromJsonAsync<MinutesSummary>();
        successor!.Key.Should().Be("MIN-2026-001"); // SAME key
        successor.Version.Should().Be(2);
        successor.Status.Should().Be("Published");

        var prior = await (await sec.GetAsync($"/api/minutes/{minutes.Key}?version=1")).Content.ReadFromJsonAsync<MinutesDetail>();
        prior!.Status.Should().Be("Superseded");
        prior.SupersededByMinutesId.Should().Be(successor.Id);
    }
}
