using Acmp.Modules.Dependencies.Application.Features.GetDependenciesRegister;
using Acmp.Modules.Dependencies.Domain;
using Acmp.Modules.Dependencies.Domain.Enums;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Modules.Traceability.Application.Features.GetArtifactRelationships;
using Acmp.Modules.Traceability.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Authorization.Abac;
using Acmp.Shared.Contracts.Topics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TraceEnums = Acmp.Modules.Traceability.Domain.Enums;

namespace Acmp.Integration.Tests;

/// <summary>
/// FR-163 / AC-114 on a REAL SQL Server — the confidentiality egress filters, executed rather than argued.
/// </summary>
/// <remarks>
/// ⚠ WHY THIS EXISTS AT ALL: DEF-066. Assigning a member stream had never once worked against a real
/// database while four InMemory suites stayed green over it, because the InMemory provider does not
/// translate anything — it runs LINQ in memory and accepts predicates SQL Server would refuse. Every
/// case in ConfidentialEgressTests runs on InMemory, so none of them can tell whether these filters
/// TRANSLATE. A predicate that silently client-evaluates would load the whole table before filtering,
/// and a predicate that fails to translate throws only here.
/// <para>
/// The two constructs under test are the ones a translation failure would land on: an enum column
/// compared to a constant, and <c>Guid[].Contains</c> over a materialised array (the IN clause).
/// </para>
/// </remarks>
[Collection(SqlBackstopCollection.Name)]
public sealed class ConfidentialEgressSqlTests
{
    private readonly SqlBackstopFixture _fx;

    public ConfidentialEgressSqlTests(SqlBackstopFixture fx) => _fx = fx;

    private static ITopicConfidentiality Hiding(params Guid[] ids)
    {
        var c = Substitute.For<ITopicConfidentiality>();
        c.GetHiddenTopicIdsAsync(Arg.Any<CancellationToken>()).Returns(ids);
        return c;
    }

    private static string UniqueKey(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..24];

    [Fact]
    public async Task The_dependency_register_filter_translates_to_SQL_and_narrows_the_paged_total()
    {
        var secret = Guid.NewGuid();
        var ordinary = Guid.NewGuid();
        var action = Guid.NewGuid();
        var secretKey = UniqueKey("DPN");
        var ordinaryKey = UniqueKey("DPN");

        await using (var seed = _fx.NewDependenciesSql())
        {
            seed.Dependencies.Add(Dependency.Create(secretKey,
                DependencyEndpointType.Topic, secret, "TOP-2026-042", "Acquire the competitor",
                DependencyEndpointType.Action, action, "ACT-2026-009", "Rotate keys",
                DependencyKind.BlockedBy, null));
            seed.Dependencies.Add(Dependency.Create(ordinaryKey,
                DependencyEndpointType.Topic, ordinary, "TOP-2026-043", "Upgrade the gateway",
                DependencyEndpointType.Action, action, "ACT-2026-009", "Rotate keys",
                DependencyKind.BlockedBy, null));
            await seed.SaveChangesAsync();
        }

        await using var db = _fx.NewDependenciesSql();

        // A large page so the assertion is about the FILTER, not about paging arithmetic.
        var query = new GetDependenciesRegisterQuery(PageSize: 500);
        var hidden = await new GetDependenciesRegisterHandler(db, Hiding(secret)).Handle(query, default);
        var full = await new GetDependenciesRegisterHandler(db, Hiding()).Handle(query, default);

        full.Items.Should().Contain(d => d.Key == secretKey);
        hidden.Items.Should().NotContain(d => d.Key == secretKey);
        hidden.Items.Should().Contain(d => d.Key == ordinaryKey);

        // ⚠ THE TOTAL IS COMPUTED BY A SEPARATE CountAsync ROUND TRIP. If the filter had been applied
        // after paging — or in memory — this number would still count the hidden row. AC-114 says
        // "absent from their totals", and on a relational provider the count is where that is decided.
        (full.Total - hidden.Total).Should().Be(1);
    }

    [Fact]
    public async Task The_relationship_filter_translates_to_SQL_on_both_endpoints()
    {
        var secret = Guid.NewGuid();
        var ordinary = Guid.NewGuid();
        var decision = Guid.NewGuid();

        await using (var seed = _fx.NewTraceabilitySql())
        {
            seed.Relationships.Add(Relationship.Create(
                TraceEnums.ArtifactType.Topic, secret, "TOP-2026-042", "Acquire the competitor",
                TraceEnums.ArtifactType.Decision, decision, "DECN-2026-007", "Approve the plan",
                TraceEnums.RelationshipType.DecidedBy, null));
            seed.Relationships.Add(Relationship.Create(
                TraceEnums.ArtifactType.Topic, ordinary, "TOP-2026-043", "Upgrade the gateway",
                TraceEnums.ArtifactType.Decision, decision, "DECN-2026-007", "Approve the plan",
                TraceEnums.RelationshipType.DecidedBy, null));
            await seed.SaveChangesAsync();
        }

        await using var db = _fx.NewTraceabilitySql();
        var handler = new GetArtifactRelationshipsHandler(db, Hiding(secret));

        // Far endpoint: the restricted topic's edge is gone from the decision's panel.
        var panel = await handler.Handle(
            new GetArtifactRelationshipsQuery(TraceEnums.ArtifactType.Decision, decision), default);
        panel.Incoming.Should().NotContain(e => e.OtherId == secret);
        panel.Incoming.Should().Contain(e => e.OtherId == ordinary);

        // Near endpoint: the restricted topic's own panel is empty, which is what removes the need for
        // a focus guard. Both legs of the predicate have now executed as SQL.
        var own = await handler.Handle(
            new GetArtifactRelationshipsQuery(TraceEnums.ArtifactType.Topic, secret), default);
        own.Outgoing.Should().BeEmpty();
        own.Incoming.Should().BeEmpty();
    }

    [Fact]
    public async Task The_confidentiality_port_resolves_the_hidden_set_against_a_real_database()
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns("kc-member");
        user.IsInRole(Arg.Any<string>()).Returns(false);

        var capabilities = Substitute.For<ITopicCapabilityResolver>();

        var restrictedKey = UniqueKey("TOP");
        var openKey = UniqueKey("TOP");
        Guid restrictedId;

        await using (var seed = _fx.NewTopicsSql())
        {
            var restricted = NewTopic(restrictedKey);
            restricted.Restrict("kc-sec", "Sara S.", DateTimeOffset.UtcNow);
            var open = NewTopic(openKey);
            seed.Topics.AddRange(restricted, open);
            await seed.SaveChangesAsync();
            restrictedId = restricted.PublicId;
        }

        capabilities.GetGrantedTopicIdsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Guid>());

        await using var db = _fx.NewTopicsSql();
        var port = new TopicConfidentialityReader(db, new TopicVisibility(user, capabilities));

        var hiddenIds = await port.GetHiddenTopicIdsAsync();

        // The port issues TWO queries and subtracts them, so this is the first proof that both halves
        // execute as SQL and that IsRestricted is a real, queryable bit column rather than a
        // client-side property the InMemory provider was happy to evaluate.
        hiddenIds.Should().Contain(restrictedId);
        await using var check = _fx.NewTopicsSql();
        var openId = await check.Topics.Where(t => t.Key == openKey).Select(t => t.PublicId).SingleAsync();
        hiddenIds.Should().NotContain(openId);
    }

    [Fact]
    public async Task A_committee_wide_reader_gets_an_empty_hidden_set_without_touching_the_database()
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns("kc-chair");
        user.IsInRole(AcmpRoles.Chairman).Returns(true);

        await using var db = _fx.NewTopicsSql();
        var port = new TopicConfidentialityReader(
            db, new TopicVisibility(user, Substitute.For<ITopicCapabilityResolver>()));

        // The short-circuit matters on a real provider: it is what keeps the common privileged read
        // from paying two extra round trips on every meeting, panel and register page they open.
        (await port.GetHiddenTopicIdsAsync()).Should().BeEmpty();
    }

    private static Topic NewTopic(string key) => Topic.Draft(
        key, "Acquire the competitor", "Consolidate the market.", "Strategic.",
        TopicType.ArchitectureDecision, TopicUrgency.Normal, TopicSource.SecurityFinding,
        "kc-omar", "Omar H.", new[] { "identity" }, Array.Empty<string>(), Array.Empty<string>());
}
