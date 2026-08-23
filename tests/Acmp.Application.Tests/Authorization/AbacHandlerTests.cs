using System.Security.Claims;
using Acmp.Shared.Authorization;
using Acmp.Shared.Authorization.Abac;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;

namespace Acmp.Application.Tests.Authorization;

// ABAC handler mechanism (docs/10 §D/§E). The AC demonstrations against real Topic/Action
// aggregates land in P5+; here the handlers are proven against stub resources/providers:
// AC-010 (stream scope), AC-009 (ownership widening), AC-011 (capability scoped to the target),
// and delegation widening (§E.3).
[Trait("Category", "Security")]
public class AbacHandlerTests
{
    private static ClaimsPrincipal Principal(string userId, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test", ClaimTypes.Name, ClaimTypes.Role));
    }

    private sealed record StubTopic(Guid TopicId, IReadOnlyCollection<string> AffectedStreams, bool AffectsAllStreams = false)
        : ITopicScopedResource, IStreamScopedResource;

    private static async Task<bool> Evaluate(IAuthorizationHandler handler, IAuthorizationRequirement req, ClaimsPrincipal user, object? resource)
    {
        var ctx = new AuthorizationHandlerContext(new[] { req }, user, resource);
        await handler.HandleAsync(ctx);
        return ctx.HasSucceeded;
    }

    // ---- Stream scope (docs/10 §E.1, AC-010) ----

    [Fact]
    public async Task Committee_wide_role_bypasses_stream_scope()
    {
        var handler = new StreamScopeHandler(Substitute.For<IUserStreamProvider>());
        var resource = new StubTopic(Guid.NewGuid(), new[] { "stream-b" });

        var allowed = await Evaluate(handler, new StreamScopeRequirement(), Principal("u", AcmpRoles.Secretary), resource);

        allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Member_in_scope_is_allowed_and_out_of_scope_is_denied()
    {
        var streams = Substitute.For<IUserStreamProvider>();
        streams.GetAssignedStreamsAsync("u", Arg.Any<CancellationToken>()).Returns(new AssignedStreams(new[] { "stream-a" }, IsUnrestricted: false));
        var handler = new StreamScopeHandler(streams);

        var inScope = new StubTopic(Guid.NewGuid(), new[] { "stream-a", "stream-b" });
        var outOfScope = new StubTopic(Guid.NewGuid(), new[] { "stream-b" });

        (await Evaluate(handler, new StreamScopeRequirement(), Principal("u", AcmpRoles.Member), inScope)).Should().BeTrue();
        (await Evaluate(handler, new StreamScopeRequirement(), Principal("u", AcmpRoles.Member), outOfScope)).Should().BeFalse();
    }

    [Fact]
    public async Task Stream_bounded_principal_without_a_subject_claim_is_denied()
    {
        var streams = Substitute.For<IUserStreamProvider>();
        var handler = new StreamScopeHandler(streams);
        // A stream-bounded role but neither a NameIdentifier nor a "sub" claim -> no user id (line 34-35).
        var noSubject = new ClaimsPrincipal(
            new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, AcmpRoles.Member) }, "Test", ClaimTypes.Name, ClaimTypes.Role));
        var resource = new StubTopic(Guid.NewGuid(), new[] { "stream-a" });

        (await Evaluate(handler, new StreamScopeRequirement(), noSubject, resource)).Should().BeFalse();
        await streams.DidNotReceive().GetAssignedStreamsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // DEF-059 / DEC-043 half two. This test previously asserted the OPPOSITE — that an empty stream
    // set is allowed — which is the fail-open it was written to describe: PUT /api/topics/{id} could
    // empty a live topic, and every stream-bounded member then passed scope on it. "Unscoped" is now
    // DECLARED, so an empty set means "affects nothing anyone holds" and denies.
    [Fact]
    public async Task Resource_with_no_affected_streams_is_denied_for_a_member()
    {
        var streams = Substitute.For<IUserStreamProvider>();
        streams.GetAssignedStreamsAsync("u", Arg.Any<CancellationToken>()).Returns(new AssignedStreams(new[] { "stream-a" }, IsUnrestricted: false));
        var handler = new StreamScopeHandler(streams);
        var resource = new StubTopic(Guid.NewGuid(), Array.Empty<string>());

        (await Evaluate(handler, new StreamScopeRequirement(), Principal("u", AcmpRoles.Member), resource)).Should().BeFalse();
    }

    // ADR-0043 clause (5): a Platform/OrgWide topic affects everything, so it belongs to no single
    // stream's members. The declaration is what grants — note the streams here are ones the member
    // does NOT hold, so the intersect would refuse and only AffectsAllStreams can be the reason.
    [Fact]
    public async Task Resource_that_declares_it_affects_all_streams_is_allowed_for_a_member()
    {
        var streams = Substitute.For<IUserStreamProvider>();
        streams.GetAssignedStreamsAsync("u", Arg.Any<CancellationToken>()).Returns(new AssignedStreams(new[] { "stream-a" }, IsUnrestricted: false));
        var handler = new StreamScopeHandler(streams);
        var resource = new StubTopic(Guid.NewGuid(), new[] { "stream-b" }, AffectsAllStreams: true);

        (await Evaluate(handler, new StreamScopeRequirement(), Principal("u", AcmpRoles.Member), resource)).Should().BeTrue();
        await streams.DidNotReceive().GetAssignedStreamsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // DEF-060: Guest is in NEITHER the committee-wide bypass list NOR §E.1's scoped set, and the
    // handler now states the scoped set positively — so a guest presenter keeps the one write
    // capability §E.3 grants them, bounded by their time window instead of by streams. Expressed as
    // the complement of the bypass list, this case was silently refused.
    [Theory]
    [InlineData(AcmpRoles.Guest)]
    [InlineData(AcmpRoles.Auditor)]
    [InlineData(AcmpRoles.Administrator)]
    public async Task A_role_outside_the_stream_bounded_set_is_never_stream_scoped(string role)
    {
        var streams = Substitute.For<IUserStreamProvider>();
        var handler = new StreamScopeHandler(streams);
        var resource = new StubTopic(Guid.NewGuid(), new[] { "stream-b" });

        (await Evaluate(handler, new StreamScopeRequirement(), Principal("u", role), resource)).Should().BeTrue();
        await streams.DidNotReceive().GetAssignedStreamsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ADR-0043 clause (3) / DW-026. The step-5 backfill assigned the WILDCARD to every member who
    // held nothing, so a handler that ignored the flag would refuse exactly the people the backfill
    // exists to protect. ⚠ The assigned CODE here deliberately does not intersect the topic, so only
    // the flag can be what allows it — and the flag comes from the column, never from the code.
    [Fact]
    public async Task A_member_holding_the_wildcard_is_unrestricted()
    {
        var streams = Substitute.For<IUserStreamProvider>();
        streams.GetAssignedStreamsAsync("u", Arg.Any<CancellationToken>())
            .Returns(new AssignedStreams(new[] { "all-streams" }, IsUnrestricted: true));
        var handler = new StreamScopeHandler(streams);
        var resource = new StubTopic(Guid.NewGuid(), new[] { "stream-b" });

        (await Evaluate(handler, new StreamScopeRequirement(), Principal("u", AcmpRoles.Member), resource)).Should().BeTrue();
    }

    // ---- Capability ownership scoped to the target topic (docs/10 §D, AC-009 / AC-011) ----

    [Fact]
    public async Task Ownership_is_scoped_to_the_specific_topic()
    {
        var ownedTopic = Guid.NewGuid();
        var otherTopic = Guid.NewGuid();
        var caps = Substitute.For<ITopicCapabilityResolver>();
        caps.GetCapabilitiesAsync("u", ownedTopic, Arg.Any<CancellationToken>()).Returns(new[] { TopicCapabilityType.Owner });
        caps.GetCapabilitiesAsync("u", otherTopic, Arg.Any<CancellationToken>()).Returns(Array.Empty<TopicCapabilityType>());

        var handler = new CapabilityHandler(caps, NoDelegation());
        var req = new CapabilityRequirement(Policies.TopicEdit, new[] { AcmpRoles.Chairman }, new[] { AcmpRoles.Member });
        var member = Principal("u", AcmpRoles.Member);

        (await Evaluate(handler, req, member, new StubTopic(ownedTopic, Array.Empty<string>()))).Should().BeTrue();
        (await Evaluate(handler, req, member, new StubTopic(otherTopic, Array.Empty<string>()))).Should().BeFalse();
    }

    // ---- Delegation widening (docs/10 §E.3) ----

    [Fact]
    public async Task Active_delegation_widens_a_policy_to_a_non_role_holder()
    {
        var delegations = Substitute.For<IDelegationResolver>();
        delegations.HasActiveDelegationAsync("u", Policies.TopicTriage, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new CapabilityHandler(Substitute.For<ITopicCapabilityResolver>(), delegations);
        var req = new CapabilityRequirement(Policies.TopicTriage, new[] { AcmpRoles.Chairman, AcmpRoles.Secretary }, Array.Empty<string>());

        var allowed = await Evaluate(handler, req, Principal("u", AcmpRoles.Member), resource: null);

        allowed.Should().BeTrue();
    }

    private static IDelegationResolver NoDelegation()
    {
        var d = Substitute.For<IDelegationResolver>();
        d.HasActiveDelegationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        return d;
    }
}
