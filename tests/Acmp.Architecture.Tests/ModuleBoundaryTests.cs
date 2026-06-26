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

    public static IEnumerable<object[]> Domains() => new[] { new object[] { MembershipDomain }, new object[] { TopicsDomain } };
    public static IEnumerable<object[]> Applications() => new[] { new object[] { MembershipApp }, new object[] { TopicsApp } };

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
                "Acmp.Modules.Topics.Application", "Acmp.Modules.Topics.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Theory]
    [MemberData(nameof(Applications))]
    public void Application_should_not_depend_on_Infrastructure(Assembly application)
    {
        var result = Types.InAssembly(application)
            .Should().NotHaveDependencyOnAny(
                "Acmp.Modules.Membership.Infrastructure", "Acmp.Modules.Topics.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact] // Modules talk only through Acmp.Shared contracts, never each other's internals (ADR-0001).
    public void Topics_should_not_depend_on_Membership()
    {
        var result = Types.InAssemblies(new[] { TopicsDomain, TopicsApp })
            .Should().NotHaveDependencyOnAny(
                "Acmp.Modules.Membership.Domain", "Acmp.Modules.Membership.Application",
                "Acmp.Modules.Membership.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void Membership_should_not_depend_on_Topics()
    {
        var result = Types.InAssemblies(new[] { MembershipDomain, MembershipApp })
            .Should().NotHaveDependencyOnAny(
                "Acmp.Modules.Topics.Domain", "Acmp.Modules.Topics.Application",
                "Acmp.Modules.Topics.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    private static string Describe(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : "Boundary violations: " + string.Join(", ", result.FailingTypeNames ?? new List<string>());
}
