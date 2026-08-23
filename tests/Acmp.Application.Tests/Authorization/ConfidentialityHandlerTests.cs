using System.Security.Claims;
using Acmp.Shared.Authorization;
using Acmp.Shared.Authorization.Abac;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;

namespace Acmp.Application.Tests.Authorization;

// C-AUTHZ-04 / FR-163 (DEC-063 d1) — the WRITE-side half of the confidentiality control.
//
// PermissionMatrixTests proves this requirement is REGISTERED and that its presence does not break the
// role matrix; it deliberately uses an unrestricted stub so it keeps measuring roles. This suite is
// where the narrowing itself is forced.
[Trait("Category", "Security")]
public class ConfidentialityHandlerTests
{
    private static readonly Guid TopicId = Guid.NewGuid();

    private static ClaimsPrincipal Principal(string userId, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test", ClaimTypes.Name, ClaimTypes.Role));
    }

    private sealed record StubTopic(Guid TopicId, bool IsRestricted)
        : ITopicScopedResource, IConfidentialResource;

    // A resource that is confidential but carries NO topic identity — grants cannot be resolved
    // against it, which is a shape the handler must refuse rather than guess at.
    private sealed record StubOpaque(bool IsRestricted) : IConfidentialResource;

    private static ITopicCapabilityResolver Capabilities(params TopicCapabilityType[] held)
    {
        var r = Substitute.For<ITopicCapabilityResolver>();
        r.GetCapabilitiesAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(held);
        return r;
    }

    private static async Task<bool> Evaluate(ClaimsPrincipal user, object? resource, ITopicCapabilityResolver caps)
    {
        var req = new ConfidentialityRequirement();
        var ctx = new AuthorizationHandlerContext(new[] { req }, user, resource);
        await new ConfidentialityHandler(caps).HandleAsync(ctx);
        return ctx.HasSucceeded;
    }

    [Fact]
    public async Task An_unrestricted_resource_succeeds_for_anyone()
    {
        // Confidentiality NARROWS, never widens: with nothing classified this requirement must be
        // inert and the principal's OTHER requirements decide the outcome.
        var granted = await Evaluate(Principal("kc-any", AcmpRoles.Member),
            new StubTopic(TopicId, IsRestricted: false), Capabilities());

        granted.Should().BeTrue();
    }

    [Theory]
    [InlineData(AcmpRoles.Chairman)]
    [InlineData(AcmpRoles.Secretary)]
    [InlineData(AcmpRoles.Auditor)]
    public async Task A_committee_wide_reader_reaches_a_restricted_resource(string role)
    {
        var granted = await Evaluate(Principal("kc-reader", role),
            new StubTopic(TopicId, IsRestricted: true), Capabilities());

        granted.Should().BeTrue();
    }

    [Theory]
    [InlineData(AcmpRoles.Member)]
    [InlineData(AcmpRoles.Reviewer)]
    [InlineData(AcmpRoles.Submitter)]
    [InlineData(AcmpRoles.Guest)]
    [InlineData(AcmpRoles.Administrator)]
    public async Task Every_other_role_without_a_grant_is_refused(string role)
    {
        // ⚠ Administrator is asserted here on purpose. It is not named in C-AUTHZ-04, and adding it
        // would be the one drift "confidentiality narrows, never widens" forbids.
        var granted = await Evaluate(Principal("kc-other", role),
            new StubTopic(TopicId, IsRestricted: true), Capabilities());

        granted.Should().BeFalse();
    }

    [Theory]
    [InlineData(TopicCapabilityType.Owner)]
    [InlineData(TopicCapabilityType.Assignee)]
    [InlineData(TopicCapabilityType.Presenter)]
    public async Task A_grantee_reaches_the_restricted_resource(TopicCapabilityType held)
    {
        // Owner arrives through the SAME mechanism as the others — grant-on-accept records ownership
        // as TopicCapabilityType.Owner — which is why the handler needs no separate owner branch.
        // Presenter matters too: it is what lets a guest presenter reach their own restricted slot.
        var granted = await Evaluate(Principal("kc-grantee", AcmpRoles.Member),
            new StubTopic(TopicId, IsRestricted: true), Capabilities(held));

        granted.Should().BeTrue();
    }

    [Fact]
    public async Task A_principal_with_no_subject_claim_is_refused()
    {
        var noSub = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Role, AcmpRoles.Member) }, "Test", ClaimTypes.Name, ClaimTypes.Role));

        var granted = await Evaluate(noSub, new StubTopic(TopicId, IsRestricted: true), Capabilities());

        granted.Should().BeFalse("a control whose job is to withhold must fail closed on a missing claim");
    }

    [Fact]
    public async Task A_confidential_resource_with_no_topic_identity_is_refused()
    {
        var granted = await Evaluate(Principal("kc-other", AcmpRoles.Member),
            new StubOpaque(IsRestricted: true), Capabilities(TopicCapabilityType.Owner));

        granted.Should().BeFalse("grants cannot be resolved without a topic id, so the safe answer is no");
    }

    [Fact]
    public async Task The_handler_is_never_invoked_without_a_matching_resource_so_the_policy_refuses()
    {
        // ⚠ DEF-068, asserted rather than described. ASP.NET cannot invoke a two-parameter handler when
        // the resource is absent, so the requirement stays unsatisfied and the policy refuses EVERYONE
        // — the Chairman included. This is why AuthorizationRegistration.ConfidentialityScoped may list
        // only policies whose every call site passes a confidential aggregate, and why TopicTriage
        // (applied at endpoint level on /close, /reopen, /reactivate, /convert) is not in it.
        var granted = await Evaluate(Principal("kc-chair", AcmpRoles.Chairman), resource: null, Capabilities());

        granted.Should().BeFalse();
    }
}
