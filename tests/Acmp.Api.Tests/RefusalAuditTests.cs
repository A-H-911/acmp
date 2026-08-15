using System.Net;
using System.Net.Http.Json;
using Acmp.Shared.Infrastructure.Audit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Api.Tests;

// DEF-056 / AC-006 / AC-003 — A REFUSAL THAT LEAVES NO TRACE.
//
// AC-006 says a denied mutation returns 403 "with an audit event emitted", and for the common case no
// event existed. It was a layering fact rather than a broken sink: every mutating endpoint carries a
// per-endpoint RequireAuthorization(Policies.X), ASP.NET evaluates it and short-circuits with 403
// BEFORE MediatR, so AuthorizationBehavior - the only thing that emitted Authorization.Forbidden -
// never ran. The AC-005/006/007 live leg measured exactly that: the 403s all arrived and the audit
// read-back returned ZERO rows.
//
// ⚠ WHY NO EXISTING TEST COULD SEE IT, which is the reusable half and the reason these are written at
// the HTTP boundary: PermissionMatrixTests evaluates the policy directly and never goes through HTTP,
// so it never reaches the middleware that short-circuits; the API tests go through HTTP but never
// asserted an audit row for a DENIAL. Before this file, the string "Authorization.Forbidden" appeared
// in no test anywhere in the solution - the emission had never been asserted by anything.
public class RefusalAuditTests
{
    private static HttpClient Client(AcmpWebApplicationFactory factory, string roles, string sub)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        return client;
    }

    private static async Task<IReadOnlyList<string>> ActionsAsync(AcmpWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        return await audit.AuditEvents.Select(e => e.Action).ToListAsync();
    }

    [Fact] // AC-006 — the refusal AND the record, asserted as a ROW rather than as an emission
    public async Task A_policy_refused_mutation_leaves_an_Authorization_Forbidden_row()
    {
        await using var factory = new AcmpWebApplicationFactory();

        // An Auditor is read-only by the permission matrix, so scheduling a meeting is refused by the
        // endpoint's own policy - before MediatR, which is the whole point.
        var response = await Client(factory, "Auditor", "kc-auditor").PostAsJsonAsync("/api/meetings", new
        {
            title = "Refused by policy",
            scheduledStart = "2030-01-01T10:00:00Z",
            scheduledEnd = "2030-01-01T11:00:00Z",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "an Auditor may not schedule meetings");

        // THE HALF THAT WAS MISSING. For a system whose purpose is an auditable committee record, an
        // unrecorded refused write is the interesting half - it is the attempt nobody can later see.
        (await ActionsAsync(factory)).Should().Contain("Authorization.Forbidden");
    }

    [Fact] // the record must not describe denials of an action nobody was identified to attempt
    public async Task An_unauthenticated_request_leaves_no_Forbidden_row()
    {
        await using var factory = new AcmpWebApplicationFactory();

        // No role/sub headers at all: the pipeline CHALLENGES rather than forbids, and that is a
        // different event. Emitting Forbidden here would fill the register with rows about a person
        // who was never identified - and Challenged is by far the more common of the two in the wild,
        // so getting this backwards would bury the real refusals rather than surface them.
        var response = await factory.CreateClient().PostAsJsonAsync("/api/meetings", new { title = "x" });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        (await ActionsAsync(factory)).Should().NotContain("Authorization.Forbidden");
    }

    [Fact] // AC-003 — the deny path that the DEF-056 middleware structurally cannot see
    public async Task A_token_with_no_committee_role_is_denied_AND_recorded()
    {
        await using var factory = new AcmpWebApplicationFactory();

        // ⚠ THIS ONE DOES NOT GO THROUGH THE POLICY LAYER AT ALL, and that is why it needs its own
        // emitter. Provisioning carries no capability policy - a caller must be able to provision
        // themselves before they hold any role - so the request PASSES authorization, reaches the
        // handler, and is refused there because the token carries no ACMP role claim to resolve.
        // AV-159 called this "DEF-056's family arriving by a second route"; a single fix for both
        // would have left AC-003's audit clause quietly unmet.
        var response = await Client(factory, "SomeUnrelatedRealmRole", "kc-roleless")
            .PostAsync("/api/members/me", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "fail-closed is the branch AC-003 takes");
        (await ActionsAsync(factory)).Should().Contain("Authorization.Forbidden");
    }

    [Fact] // the control: a caller who IS allowed writes no refusal row
    public async Task An_allowed_caller_leaves_no_refusal_row()
    {
        await using var factory = new AcmpWebApplicationFactory();

        // Without this, every assertion above is equally consistent with "this route refuses everyone"
        // or "something emits Authorization.Forbidden on every request". A refusal record that fires
        // for permitted actions is not an audit trail, it is noise that hides the real ones.
        var response = await Client(factory, "Secretary", "kc-sec").PostAsJsonAsync("/api/meetings", new
        {
            title = "Allowed by policy",
            scheduledStart = "2030-01-01T10:00:00Z",
            scheduledEnd = "2030-01-01T11:00:00Z",
        });

        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        (await ActionsAsync(factory)).Should().NotContain("Authorization.Forbidden");
    }
}
