using System.Reflection;
using Acmp.Shared.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Architecture.Tests;

/*
 * DW-026, THE NARROW CHECK — every IAuthorizationRequirement appears in at least one REGISTERED POLICY.
 *
 * DW-026 describes a class of defect where a capability exists, is correct, is commented, and in two
 * cases is unit-tested and passing — and is WIRED TO NOTHING. It has fired five times. Its own row
 * advises doing this narrow check FIRST, because it is a handful of lines and it guards the only
 * instance rated HIGH.
 *
 * THAT INSTANCE IS DEF-057, AND IT IS WORTH STATING PRECISELY BECAUSE IT EXPLAINS THE SHAPE OF THIS
 * TEST. StreamScopeHandler was registered in DI, unit-tested four separate ways, and present in NO
 * POLICY. Nothing in the build objected: the compiler is satisfied (a registered handler needs no
 * policy), the unit tests pass (they invoke the handler directly, which is exactly why they pass),
 * and coverage is satisfied (the handler is covered — by its own test). The control was therefore
 * never EVALUATED, so it failed OPEN, and a specified authorization control that is never evaluated
 * is indistinguishable from an implemented one (INV-005, permission-role-matrix §E.1).
 *
 * ⚠ WHY THIS DRIVES THE REAL REGISTRATION RATHER THAN RE-DECLARING THE MATRIX: the defect being
 * guarded is a MISMATCH between what is registered and what is reachable. A test that re-listed the
 * expected requirements would agree with itself and catch nothing — the same reason
 * PermissionMatrixTests encodes its expected cells independently of AuthorizationRegistration's
 * table. So this builds the container the host builds, asks the policy provider for each policy the
 * product actually registers, and reads the requirements back out.
 *
 * ⚠ DISCOVERY IS BY REFLECTION OVER THE ASSEMBLY, NOT A HARD-CODED LIST. A list would have to be
 * updated by the same person who forgot to wire the requirement up, which is precisely the failure
 * mode; reflection means a NEW requirement type is covered the moment it compiles.
 */
public class AuthorizationCoverageTests
{
    private static readonly Assembly SharedKernel = typeof(CapabilityRequirement).Assembly;

    /// <summary>The concrete requirement types the product defines, discovered rather than listed.</summary>
    private static Type[] DeclaredRequirements() => SharedKernel
        .GetTypes()
        .Where(t => typeof(IAuthorizationRequirement).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
        .ToArray();

    /// <summary>Every requirement type reachable through a registered policy, via the real container.</summary>
    private static async Task<HashSet<Type>> WiredRequirementsAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddAcmpAuthorization(new ConfigurationBuilder().Build());

        await using var provider = services.BuildServiceProvider();
        var policies = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        var wired = new HashSet<Type>();
        foreach (var name in AuthorizationRegistration.RegisteredPolicies)
        {
            var policy = await policies.GetPolicyAsync(name);
            policy.Should().NotBeNull($"policy '{name}' is advertised by RegisteredPolicies but the provider does not know it");
            foreach (var requirement in policy!.Requirements)
                wired.Add(requirement.GetType());
        }

        return wired;
    }

    [Fact]
    public async Task Every_authorization_requirement_is_reachable_through_a_registered_policy()
    {
        var declared = DeclaredRequirements();
        var wired = await WiredRequirementsAsync();

        var orphans = declared.Where(t => !wired.Contains(t)).Select(t => t.FullName).ToArray();

        // Named, not counted. An orphaned requirement FAILS OPEN — the policy that should carry it
        // simply never evaluates it — so the message says what the silence means, not just that a
        // number was wrong.
        orphans.Should().BeEmpty(
            "an IAuthorizationRequirement in no registered policy is NEVER EVALUATED, so the control "
            + "it encodes fails OPEN and is indistinguishable from an implemented one (DEF-057). "
            + "Add it to a policy in AuthorizationRegistration, or delete it:\n  "
            + string.Join("\n  ", orphans));
    }

    [Fact]
    public async Task The_check_has_a_subject_on_both_sides()
    {
        // The other half of "cannot pass vacuously", and it is not theoretical: if the reflection scan
        // ever stopped finding requirement types — a namespace move, a assembly split — the assertion
        // above would pass over an EMPTY set while checking nothing, which is exactly the hollow pass
        // this project has now recorded three times.
        DeclaredRequirements().Should().NotBeEmpty("the reflection scan must actually find requirement types");
        (await WiredRequirementsAsync()).Should().NotBeEmpty("the policy walk must actually reach requirements");
        AuthorizationRegistration.RegisteredPolicies.Should().NotBeEmpty("the product must register policies at all");
    }
}
