using Acmp.Modules.Meetings.Domain;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Shared.Domain.ValueObjects;
using Acmp.Shared.Infrastructure.Audit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Api.Tests;

// C-AUTH-05 SoD-2 (NFR-064), warn-and-audit BY SPEC: approving minutes you solely authored is ALLOWED,
// flagged on the aggregate, and recorded as a distinct AuditEvent.
//
// ⚠⚠ WHY THIS FILE EXISTS WHEN SoD-2 IS ALREADY TESTED. MinutesHandlerTests covers the behaviour well - the
// flag, a paired negative case, and the emission - but it asserts the audit through a MOCK:
// `audit.Received(1).EmitEnrichedAsync(...)`. NFR-064's verification detail asks, for a warn-and-audit rule
// specifically, for "the warning AND the resulting AuditEvent as a ROW". A mock proves the handler called
// the sink; it cannot prove a row was written, and it passes unchanged if the sink is misconfigured, the
// column is wrong, or the write is rolled back. Measured 2026-08-29 while sizing WBS-26.1, SoD-2 was the one
// rule of the five whose evidence fell short of that bar - SoD-1, SoD-3 and SoD-5 are hard rules whose bar
// is a FORCED refusal, and all three force one. DEC-096 d3 put the gap inside this item.
//
// ⛔ IT DOES NOT REPLACE MinutesHandlerTests' CASE. That one owns the negative half (a different approver
// clears the flag) and the read-model round trip. This one adds the row, which is the half nothing asserted.
public class Sod2SoleAuthorAuditRowTests
{
    private const string SoleAuthorEvent = "Meetings.MinutesApprovedBySoleAuthor";
    private const string ApprovedEvent = "Meetings.MinutesApproved";

    // ⚠ `Action ?? EventType` — the coalesce RefusalAuditTests documents at length. Selecting Action alone
    // returns null for every lean v1 row, which reads exactly like the feature being absent.
    private static async Task<IReadOnlyList<string>> ActionsAsync(AcmpWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        return await audit.AuditEvents.Select(e => e.Action ?? e.EventType).ToListAsync();
    }

    private static HttpClient Client(AcmpWebApplicationFactory factory, string roles, string sub)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        return client;
    }

    // Drafts minutes AS <authorSub> and walks them to InReview, through the real DbContext and the real
    // audit sink, so the approval below writes rows rather than exercising a double.
    private static async Task<Guid> MinutesInReviewAsync(AcmpWebApplicationFactory factory, string authorSub)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();

        var minutes = MinutesOfMeeting.Draft(
            "MIN-2026-900", Guid.NewGuid(), "MTG-2026-900", "Committee session",
            LocalizedString.Create("Roadmap discussed", "نوقشت خارطة الطريق"),
            DateTimeOffset.UtcNow);

        minutes.SubmitForReview(DateTimeOffset.UtcNow);
        db.Minutes.Add(minutes);
        await db.SaveChangesAsync();

        // ⚠⚠ CreatedBy IS SET AFTER THE INSERT, AND THE ORDER IS NOT COSMETIC. SoD-2 keys off CreatedBy, and
        // ModuleDbContext.StampAudit OVERWRITES it from ICurrentUser for every entry in state Added - so
        // assigning it before SaveChanges is silently clobbered by the ambient principal, which is not the
        // author this test needs. On a MODIFIED entry the same stamp touches UpdatedAt/UpdatedBy only and
        // leaves CreatedBy alone, so a second save makes the precondition stick.
        //
        // This is not a hypothetical: the first version of this test set it before the insert, and the
        // sole-author assertion failed with only the ordinary approval row present. It failed LOUDLY, which
        // is the good case - a precondition that quietly does not hold is how a test ends up proving nothing.
        minutes.CreatedBy = authorSub;
        await db.SaveChangesAsync();

        return minutes.PublicId;
    }

    [Fact] // NFR-064 SoD-2: the warning is ALLOWED, and it leaves a row
    public async Task Approving_your_own_sole_authored_minutes_is_allowed_and_leaves_a_sole_author_row()
    {
        await using var factory = new AcmpWebApplicationFactory();
        const string author = "kc-sam";
        var id = await MinutesInReviewAsync(factory, author);

        var response = await Client(factory, "Secretary", author)
            .PostAsync($"/api/minutes/{id}/approve", null);

        // WARN, NOT REFUSE — SoD-2 is soft by spec, and a 403 here would mean it had been hardened.
        response.IsSuccessStatusCode.Should().BeTrue();

        var actions = await ActionsAsync(factory);
        actions.Should().Contain(SoleAuthorEvent);
        actions.Should().Contain(ApprovedEvent, "the ordinary approval is audited too, not replaced");
    }

    [Fact] // the discriminator: a different approver leaves the ordinary row and NOT the sole-author one
    public async Task Approving_someone_elses_minutes_leaves_no_sole_author_row()
    {
        await using var factory = new AcmpWebApplicationFactory();
        var id = await MinutesInReviewAsync(factory, "kc-sam");

        var response = await Client(factory, "Chairman", "kc-chair")
            .PostAsync($"/api/minutes/{id}/approve", null);

        response.IsSuccessStatusCode.Should().BeTrue();

        var actions = await ActionsAsync(factory);
        // ⚠ THE CONTROL ON THE CONTROL. A NotContain over an empty list passes vacuously; RefusalAuditTests
        // records exactly that failure. Asserting the ordinary row proves the audit path ran, so the absence
        // below means the RULE declined to fire rather than nothing being written.
        actions.Should().Contain(ApprovedEvent);
        actions.Should().NotContain(SoleAuthorEvent);
    }
}
