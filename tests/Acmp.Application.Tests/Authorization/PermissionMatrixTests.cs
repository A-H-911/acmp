using System.Security.Claims;
using Acmp.Shared.Authorization;
using Acmp.Shared.Authorization.Abac;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Acmp.Application.Tests.Authorization;

// The permission-matrix suite (docs/10 §C). The expected A / AiO / D verdicts below are transcribed
// INDEPENDENTLY from the document — they are NOT read from the policy registry — so this test
// proves the registered policies match the matrix rather than matching themselves (centerpiece).
// Covers AC-005/006 (RBAC deny), AC-007 (SoD-5: Administrator denied on every committee-content row),
// and the AiO/ownership dimension for AC-009.
[Trait("Category", "Security")]
public class PermissionMatrixTests
{
    // Role columns in docs/10 §C order.
    private static readonly string[] Roles =
    {
        AcmpRoles.Chairman, AcmpRoles.Secretary, AcmpRoles.Member, AcmpRoles.Reviewer,
        AcmpRoles.Auditor, AcmpRoles.Administrator, AcmpRoles.Submitter, AcmpRoles.Guest,
    };

    // 'A' = allow · 'O' = allow-if-owner · 'D' = deny. One char per role column above.
    private static readonly Dictionary<string, string> Expected = new()
    {
        [Policies.TopicSubmit] = "AAAADDAD",
        [Policies.TopicTriage] = "AADDDDDD",
        [Policies.TopicEdit] = "AAOODDOD",
        [Policies.BacklogPrioritize] = "AADDDDDD",
        [Policies.AgendaPublish] = "AADDDDDD",
        [Policies.MeetingSchedule] = "AADDDDDD",
        [Policies.AttendanceRecord] = "AADDDDDD",
        [Policies.MinutesCapture] = "AADDDDDD",
        [Policies.MinutesApprove] = "AADDDDDD",
        [Policies.VoteManage] = "AADDDDDD",
        [Policies.VoteCast] = "ADADDDDD",
        [Policies.DecisionRecord] = "AADDDDDD",
        [Policies.DecisionChairApprove] = "ADDDDDDD",
        [Policies.ActionCreate] = "AAODDDDD",
        [Policies.ActionVerify] = "AAODDDDD",
        [Policies.RiskManage] = "AAOODDDD",
        [Policies.RiskAccept] = "AADDDDDD",
        [Policies.DependencyCreate] = "AAOODDDD",
        [Policies.TraceabilityLink] = "AADDDDDD",
        [Policies.AdrCreate] = "AAOODDDD",
        [Policies.AdrApprove] = "AADDDDDD",
        [Policies.AdrPromote] = "ADDDDDDD",
        [Policies.AdrSupersede] = "AADDDDDD",
        [Policies.InvariantCreate] = "AAOODDDD",
        [Policies.InvariantApprove] = "AADDDDDD",
        [Policies.TemplateManage] = "AADDDADD",
        [Policies.DocumentManage] = "AAOODDDD",
        [Policies.DiagramAttach] = "AAOODDOO",
        [Policies.ResearchManage] = "AAOODDDD",
        [Policies.AdminUsers] = "DDDDDADD",
        [Policies.AuthDelegate] = "AADDDDDD",
        [Policies.AuditRead] = "AADDADDD",
        [Policies.ReportExport] = "AAAAADOD",
        [Policies.AdminConfig] = "DDDDDADD",
    };

    public static IEnumerable<object[]> Cases()
    {
        foreach (var (policy, cells) in Expected)
            for (var i = 0; i < Roles.Length; i++)
                yield return new object[] { policy, Roles[i], cells[i] };
    }

    [Fact]
    public void Every_registered_policy_has_an_expected_row()
    {
        Expected.Keys.Should().BeEquivalentTo(AuthorizationRegistration.RegisteredPolicies);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Policy_decision_matches_the_matrix(string policy, string role, char verdict)
    {
        var ownedProvider = BuildProvider(ownsResource: true);
        await using var _ = ownedProvider;

        // A: granted by role alone (no resource needed).
        var withoutResource = await AuthorizeAsync(ownedProvider, role, policy, resource: null);
        // O/D distinguished by whether an owned target widens the decision.
        var withOwnedResource = await AuthorizeAsync(ownedProvider, role, policy, new StubTopic(Guid.NewGuid()));

        switch (verdict)
        {
            case 'A':
                withoutResource.Should().BeTrue($"{role} holds {policy} outright");
                break;
            case 'O':
                withoutResource.Should().BeFalse($"{role} needs a relationship for {policy}");
                withOwnedResource.Should().BeTrue($"{role} owns the target so {policy} is allowed");
                break;
            case 'D':
                withoutResource.Should().BeFalse($"{role} is denied {policy}");
                withOwnedResource.Should().BeFalse($"ownership never widens a Deny for {role} on {policy}");
                break;
        }
    }

    private static async Task<bool> AuthorizeAsync(ServiceProvider provider, string role, string policy, object? resource)
    {
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var principal = Principal(role);
        var result = resource is null
            ? await service.AuthorizeAsync(principal, policy)
            : await service.AuthorizeAsync(principal, resource, policy);
        return result.Succeeded;
    }

    private static ServiceProvider BuildProvider(bool ownsResource)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var capabilities = Substitute.For<ITopicCapabilityResolver>();
        capabilities.GetCapabilitiesAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ownsResource ? new[] { TopicCapabilityType.Owner } : Array.Empty<TopicCapabilityType>());
        var delegations = Substitute.For<IDelegationResolver>();
        delegations.HasActiveDelegationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var streams = Substitute.For<IUserStreamProvider>();

        services.AddSingleton(capabilities);
        services.AddSingleton(delegations);
        services.AddSingleton(streams);
        services.AddAcmpAuthorization(new ConfigurationBuilder().Build());

        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal Principal(string role) =>
        new(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "user-1"), new Claim(ClaimTypes.Role, role) },
            authenticationType: "Test", nameType: ClaimTypes.Name, roleType: ClaimTypes.Role));

    private sealed record StubTopic(Guid TopicId) : ITopicScopedResource;
}
