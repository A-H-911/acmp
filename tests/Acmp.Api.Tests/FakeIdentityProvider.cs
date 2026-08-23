using System.Collections.Concurrent;
using Acmp.Modules.Membership.Application.Abstractions;

namespace Acmp.Api.Tests;

// Stands in for Keycloak's Admin API so the INVITE HAPPY PATH is reachable from the API harness.
//
// ⚠ WHY THIS IS OPT-IN AND NOT THE DEFAULT (SC-005, third occurrence). The host registers
// IIdentityProvider only when the Keycloak admin client is configured, and
// UserManagementApiTests.An_authorised_caller_still_cannot_invite_when_no_identity_provider_is_configured
// asserts that ABSENCE deliberately: an invite must fail at composition rather than create a member
// row for a person with no account, because DEF-029 means that row can be disabled but never deleted.
// Registering a fake by default would quietly delete that property. So the default factory is
// unchanged and a test that wants the happy path asks for it by name.
//
// It records what it was asked to do, so a test can assert the Keycloak side actually happened
// instead of inferring it from the local row.
public sealed class FakeIdentityProvider : IIdentityProvider
{
    public ConcurrentBag<(string Email, string FullName)> Created { get; } = new();
    public ConcurrentBag<string> Disabled { get; } = new();
    public ConcurrentBag<string> Enabled { get; } = new();
    public ConcurrentBag<string> SignedOut { get; } = new();
    public ConcurrentDictionary<string, IReadOnlyCollection<string>> Roles { get; } = new();

    /// <summary>The realm as a test wants it seen (SC-011). Empty unless a test populates it.</summary>
    public List<IdentityAccount> Accounts { get; } = new();

    public Task<InvitedAccount> CreateUserAsync(string email, string fullName, CancellationToken ct = default)
    {
        Created.Add((email, fullName));
        // Deterministic from the email so a test can predict the subject without reading it back.
        return Task.FromResult(new InvitedAccount($"kc-{email}", "Temp-Passw0rd!"));
    }

    public Task SetRealmRolesAsync(string subjectId, IReadOnlyCollection<string> roles, CancellationToken ct = default)
    {
        Roles[subjectId] = roles;
        return Task.CompletedTask;
    }

    public Task SignOutEverywhereAsync(string subjectId, CancellationToken ct = default)
    {
        SignedOut.Add(subjectId);
        return Task.CompletedTask;
    }

    public Task DisableUserAsync(string subjectId, CancellationToken ct = default)
    {
        Disabled.Add(subjectId);
        return Task.CompletedTask;
    }

    // SC-017. Recorded separately from Disabled rather than removing from it, so a test can assert
    // the enable ACTUALLY HAPPENED instead of inferring it from an absence — an absence is only
    // evidence if the instrument is proven present.
    public Task EnableUserAsync(string subjectId, CancellationToken ct = default)
    {
        Enabled.Add(subjectId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IdentityAccount>> ListUsersAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<IdentityAccount>>(Accounts.ToArray());
}
