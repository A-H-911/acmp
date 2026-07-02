using System.Collections.Generic;
using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Acmp.Architecture.Tests;

// Enforces Clean-Architecture-per-module and module isolation at build time (docs/34 §5.1, ADR-0001).
// Parameterized over every module so the rules scale as modules are added.
public class ModuleBoundaryTests
{
    private static readonly Assembly MembershipDomain = typeof(Acmp.Modules.Membership.Domain.CommitteeMember).Assembly;
    private static readonly Assembly MembershipApp = typeof(Acmp.Modules.Membership.Application.MembershipApplicationExtensions).Assembly;
    private static readonly Assembly TopicsDomain = typeof(Acmp.Modules.Topics.Domain.Topic).Assembly;
    private static readonly Assembly TopicsApp = typeof(Acmp.Modules.Topics.Application.TopicsApplicationExtensions).Assembly;
    private static readonly Assembly MeetingsDomain = typeof(Acmp.Modules.Meetings.Domain.Meeting).Assembly;
    private static readonly Assembly MeetingsApp = typeof(Acmp.Modules.Meetings.Application.MeetingsApplicationExtensions).Assembly;
    private static readonly Assembly NotificationsDomain = typeof(Acmp.Modules.Notifications.Domain.Notification).Assembly;
    private static readonly Assembly NotificationsApp = typeof(Acmp.Modules.Notifications.Application.NotificationsApplicationExtensions).Assembly;
    private static readonly Assembly DecisionsDomain = typeof(Acmp.Modules.Decisions.Domain.Decision).Assembly;
    private static readonly Assembly DecisionsApp = typeof(Acmp.Modules.Decisions.Application.DecisionsApplicationExtensions).Assembly;
    private static readonly Assembly ActionsDomain = typeof(Acmp.Modules.Actions.Domain.ActionItem).Assembly;
    private static readonly Assembly ActionsApp = typeof(Acmp.Modules.Actions.Application.ActionsApplicationExtensions).Assembly;
    private static readonly Assembly RisksDomain = typeof(Acmp.Modules.Risks.Domain.Risk).Assembly;
    private static readonly Assembly RisksApp = typeof(Acmp.Modules.Risks.Application.RisksApplicationExtensions).Assembly;

    public static IEnumerable<object[]> Domains() => new[] { new object[] { MembershipDomain }, new object[] { TopicsDomain }, new object[] { MeetingsDomain }, new object[] { NotificationsDomain }, new object[] { DecisionsDomain }, new object[] { ActionsDomain }, new object[] { RisksDomain } };
    public static IEnumerable<object[]> Applications() => new[] { new object[] { MembershipApp }, new object[] { TopicsApp }, new object[] { MeetingsApp }, new object[] { NotificationsApp }, new object[] { DecisionsApp }, new object[] { ActionsApp }, new object[] { RisksApp } };

    [Theory]
    [MemberData(nameof(Domains))]
    public void Domain_should_not_depend_on_EntityFrameworkCore(Assembly domain)
    {
        var result = Types.InAssembly(domain)
            .Should().NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Theory]
    [MemberData(nameof(Domains))]
    public void Domain_should_not_depend_on_Application_or_Infrastructure(Assembly domain)
    {
        var result = Types.InAssembly(domain)
            .Should().NotHaveDependencyOnAny(
                "Acmp.Modules.Membership.Application", "Acmp.Modules.Membership.Infrastructure",
                "Acmp.Modules.Topics.Application", "Acmp.Modules.Topics.Infrastructure",
                "Acmp.Modules.Meetings.Application", "Acmp.Modules.Meetings.Infrastructure",
                "Acmp.Modules.Notifications.Application", "Acmp.Modules.Notifications.Infrastructure",
                "Acmp.Modules.Decisions.Application", "Acmp.Modules.Decisions.Infrastructure",
                "Acmp.Modules.Actions.Application", "Acmp.Modules.Actions.Infrastructure",
                "Acmp.Modules.Risks.Application", "Acmp.Modules.Risks.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Theory]
    [MemberData(nameof(Applications))]
    public void Application_should_not_depend_on_Infrastructure(Assembly application)
    {
        var result = Types.InAssembly(application)
            .Should().NotHaveDependencyOnAny(
                "Acmp.Modules.Membership.Infrastructure", "Acmp.Modules.Topics.Infrastructure",
                "Acmp.Modules.Meetings.Infrastructure", "Acmp.Modules.Notifications.Infrastructure",
                "Acmp.Modules.Decisions.Infrastructure", "Acmp.Modules.Actions.Infrastructure",
                "Acmp.Modules.Risks.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact] // Modules talk only through Acmp.Shared contracts, never each other's internals (ADR-0001).
    public void Topics_should_not_depend_on_Membership()
    {
        var result = Types.InAssemblies(new[] { TopicsDomain, TopicsApp })
            .Should().NotHaveDependencyOnAny(
                "Acmp.Modules.Membership.Domain", "Acmp.Modules.Membership.Application",
                "Acmp.Modules.Membership.Infrastructure",
                "Acmp.Modules.Meetings.Domain", "Acmp.Modules.Meetings.Application",
                "Acmp.Modules.Meetings.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void Membership_should_not_depend_on_Topics()
    {
        var result = Types.InAssemblies(new[] { MembershipDomain, MembershipApp })
            .Should().NotHaveDependencyOnAny(
                "Acmp.Modules.Topics.Domain", "Acmp.Modules.Topics.Application",
                "Acmp.Modules.Topics.Infrastructure",
                "Acmp.Modules.Meetings.Domain", "Acmp.Modules.Meetings.Application",
                "Acmp.Modules.Meetings.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact] // Meetings reaches Topics (ITopicScheduler), Membership (ICommitteeDirectory), and the
           // notification channel only through Acmp.Shared contracts — never another module's assemblies.
    public void Meetings_should_not_depend_on_other_modules()
    {
        var result = Types.InAssemblies(new[] { MeetingsDomain, MeetingsApp })
            .Should().NotHaveDependencyOnAny(
                "Acmp.Modules.Topics.Domain", "Acmp.Modules.Topics.Application", "Acmp.Modules.Topics.Infrastructure",
                "Acmp.Modules.Membership.Domain", "Acmp.Modules.Membership.Application", "Acmp.Modules.Membership.Infrastructure",
                "Acmp.Modules.Notifications.Domain", "Acmp.Modules.Notifications.Application", "Acmp.Modules.Notifications.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact] // Decisions reaches Topics (ITopicDecisionRecorder), Membership (ICommitteeDirectory), and the
           // notification channel only through Acmp.Shared contracts — never another module's assemblies.
    public void Decisions_should_not_depend_on_other_modules()
    {
        var result = Types.InAssemblies(new[] { DecisionsDomain, DecisionsApp })
            .Should().NotHaveDependencyOnAny(
                "Acmp.Modules.Topics.Domain", "Acmp.Modules.Topics.Application", "Acmp.Modules.Topics.Infrastructure",
                "Acmp.Modules.Membership.Domain", "Acmp.Modules.Membership.Application", "Acmp.Modules.Membership.Infrastructure",
                "Acmp.Modules.Meetings.Domain", "Acmp.Modules.Meetings.Application", "Acmp.Modules.Meetings.Infrastructure",
                "Acmp.Modules.Notifications.Domain", "Acmp.Modules.Notifications.Application", "Acmp.Modules.Notifications.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact] // Actions reaches Membership/Notifications only through Acmp.Shared contracts (the committee
           // directory + the notification channel) — never another module's assemblies (ADR-0001). The P8d
           // decision-link seam will likewise be a Shared contract, not a Decisions reference.
    public void Actions_should_not_depend_on_other_modules()
    {
        var result = Types.InAssemblies(new[] { ActionsDomain, ActionsApp })
            .Should().NotHaveDependencyOnAny(
                "Acmp.Modules.Topics.Domain", "Acmp.Modules.Topics.Application", "Acmp.Modules.Topics.Infrastructure",
                "Acmp.Modules.Membership.Domain", "Acmp.Modules.Membership.Application", "Acmp.Modules.Membership.Infrastructure",
                "Acmp.Modules.Meetings.Domain", "Acmp.Modules.Meetings.Application", "Acmp.Modules.Meetings.Infrastructure",
                "Acmp.Modules.Decisions.Domain", "Acmp.Modules.Decisions.Application", "Acmp.Modules.Decisions.Infrastructure",
                "Acmp.Modules.Notifications.Domain", "Acmp.Modules.Notifications.Application", "Acmp.Modules.Notifications.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact] // Risks reaches Membership (ICommitteeDirectory, escalation fan-out) and the notification channel
           // only through Acmp.Shared contracts — never another module's assemblies (ADR-0001).
    public void Risks_should_not_depend_on_other_modules()
    {
        var result = Types.InAssemblies(new[] { RisksDomain, RisksApp })
            .Should().NotHaveDependencyOnAny(
                "Acmp.Modules.Topics.Domain", "Acmp.Modules.Topics.Application", "Acmp.Modules.Topics.Infrastructure",
                "Acmp.Modules.Membership.Domain", "Acmp.Modules.Membership.Application", "Acmp.Modules.Membership.Infrastructure",
                "Acmp.Modules.Meetings.Domain", "Acmp.Modules.Meetings.Application", "Acmp.Modules.Meetings.Infrastructure",
                "Acmp.Modules.Decisions.Domain", "Acmp.Modules.Decisions.Application", "Acmp.Modules.Decisions.Infrastructure",
                "Acmp.Modules.Actions.Domain", "Acmp.Modules.Actions.Application", "Acmp.Modules.Actions.Infrastructure",
                "Acmp.Modules.Notifications.Domain", "Acmp.Modules.Notifications.Application", "Acmp.Modules.Notifications.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact] // Notifications is a leaf — it depends on nothing but Acmp.Shared (the channel + the message contract).
    public void Notifications_should_not_depend_on_other_modules()
    {
        var result = Types.InAssemblies(new[] { NotificationsDomain, NotificationsApp })
            .Should().NotHaveDependencyOnAny(
                "Acmp.Modules.Membership.Domain", "Acmp.Modules.Membership.Application", "Acmp.Modules.Membership.Infrastructure",
                "Acmp.Modules.Topics.Domain", "Acmp.Modules.Topics.Application", "Acmp.Modules.Topics.Infrastructure",
                "Acmp.Modules.Meetings.Domain", "Acmp.Modules.Meetings.Application", "Acmp.Modules.Meetings.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    private static string Describe(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : "Boundary violations: " + string.Join(", ", result.FailingTypeNames ?? new List<string>());
}
