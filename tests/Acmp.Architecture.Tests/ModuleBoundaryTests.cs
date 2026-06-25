using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Acmp.Architecture.Tests;

// Enforces Clean-Architecture-per-module and module isolation at build time (docs/34 section 5.1).
// These rules scale as modules are added; with one module today they already guard the layering.
public class ModuleBoundaryTests
{
    private static readonly Assembly DomainAsm =
        typeof(Acmp.Modules.Membership.Domain.CommitteeMember).Assembly;
    private static readonly Assembly ApplicationAsm =
        typeof(Acmp.Modules.Membership.Application.MembershipApplicationExtensions).Assembly;

    [Fact]
    public void Domain_should_not_depend_on_EntityFrameworkCore()
    {
        var result = Types.InAssembly(DomainAsm)
            .Should().NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void Domain_should_not_depend_on_Application_or_Infrastructure()
    {
        var result = Types.InAssembly(DomainAsm)
            .Should().NotHaveDependencyOnAny(
                "Acmp.Modules.Membership.Application",
                "Acmp.Modules.Membership.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    [Fact]
    public void Application_should_not_depend_on_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAsm)
            .Should().NotHaveDependencyOn("Acmp.Modules.Membership.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Describe(result));
    }

    private static string Describe(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : "Boundary violations: " + string.Join(", ", result.FailingTypeNames ?? new List<string>());
}
