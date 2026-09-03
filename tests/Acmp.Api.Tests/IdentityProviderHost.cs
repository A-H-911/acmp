namespace Acmp.Api.Tests;

/// <summary>
/// The SECOND class fixture <c>DEC-124</c> d1 called for: one shared host built by
/// <see cref="AcmpWebApplicationFactory.WithIdentityProvider"/>, for the classes that need the fake
/// Keycloak.
/// </summary>
/// <remarks>
/// <para>
/// ⚠ WHY A WRAPPER RATHER THAN A SECOND FACTORY TYPE. xUnit activates an <c>IClassFixture&lt;T&gt;</c>
/// through <c>T</c>'s parameterless constructor, and <see cref="AcmpWebApplicationFactory"/>'s
/// parameterless constructor is the one that builds the DEFAULT host — deliberately, since
/// <c>SC-005</c> requires the no-identity-provider composition to stay the default and an existing test
/// asserts an invite FAILS without one. The factory is <c>sealed</c> and <c>UseIdentityProvider</c> is
/// <c>private init</c>, so a subclass cannot reach it either, and unsealing to get one would put a
/// second constructor next to the comment warning that adding one failed 22 tests at construction time.
/// Holding the named factory in a field costs one small type and touches none of that.
/// </para>
/// <para>
/// ⛔ THE HOST IS SHARED; THE RECORDED CALLS ARE NOT. <see cref="FakeIdentityProvider"/> accumulates
/// into <c>ConcurrentBag</c>s that tests assert over, so every class taking this fixture must call
/// <see cref="FakeIdentityProvider.Reset"/> in its own constructor — which xUnit runs once per test
/// method even though this fixture is built once per class. See that method's remarks for what happens
/// if it does not.
/// </para>
/// </remarks>
public sealed class IdentityProviderHost : IDisposable
{
    public AcmpWebApplicationFactory Factory { get; } = AcmpWebApplicationFactory.WithIdentityProvider();

    /// <summary>
    /// The host with empty databases and no recorded Keycloak calls, for a test class constructor.
    /// One call, because <see cref="AcmpWebApplicationFactory.Reset"/> already clears the fake when the
    /// host has one — keeping the two resets in one place rather than in two that can disagree.
    /// </summary>
    public AcmpWebApplicationFactory Fresh() => Factory.Reset();

    public void Dispose() => Factory.Dispose();
}
