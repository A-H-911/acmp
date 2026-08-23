using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Acmp.Modules.Membership.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Acmp.Modules.Membership.Infrastructure.Identity;

// ADR-0038 — the ONLY code in this application that calls Keycloak's Admin REST API. Before this
// there were zero callers anywhere in src/, so the surface is kept to the operations the port
// declares — four writes and, since SC-011, one read; there is deliberately no general-purpose
// passthrough, because the blast radius of the service-account credential is bounded by what this
// class can do with it.
//
// The service account must hold the MINIMUM realm-management roles for these calls and must
// NOT hold realm-admin. That set is to be PROVEN rather than assumed from documentation
// (ADR-0038), and a narrower grant being refused is a signal to look at the refusal, not to widen it.
//
// ⚠ THE {manage-users} GRANT COVERS THE READ TOO — MEASURED, NOT INFERRED. Against Keycloak 26.0
// with the service account holding exactly the deploy/keycloak/admin-client.env grant and nothing
// else: GET /users → 200 and GET /users/{id}/role-mappings/realm → 200. So SC-011 widens the port
// without widening the grant, which is the outcome that made it cheap. See ListUsersAsync for the
// shape that grant REFUSES.
public sealed class KeycloakAdminClient : IIdentityProvider
{
    private readonly HttpClient _http;
    private readonly KeycloakAdminOptions _options;

    public KeycloakAdminClient(HttpClient http, IOptions<KeycloakAdminOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<InvitedAccount> CreateUserAsync(string email, string fullName, CancellationToken ct = default)
    {
        await AuthenticateAsync(ct);

        var (first, last) = SplitName(fullName);
        var create = await _http.PostAsJsonAsync(
            $"admin/realms/{_options.Realm}/users",
            new
            {
                username = email,
                email,
                firstName = first,
                lastName = last,
                enabled = true,
                emailVerified = false,
            },
            ct);

        // 409 here means the account exists in Keycloak but not in ACMP's roster — a real state, and
        // one the caller must be able to tell apart from its own duplicate check.
        if (create.StatusCode == System.Net.HttpStatusCode.Conflict)
            throw new InvalidOperationException($"An identity-provider account already exists for {email}.");
        create.EnsureSuccessStatusCode();

        // Keycloak returns the new id in the Location header, not the body.
        var subjectId = create.Headers.Location?.Segments.LastOrDefault()?.Trim('/')
            ?? throw new InvalidOperationException("Keycloak did not return a Location for the created user.");

        var temporaryPassword = GeneratePassword();
        var reset = await _http.PutAsJsonAsync(
            $"admin/realms/{_options.Realm}/users/{subjectId}/reset-password",
            new { type = "password", value = temporaryPassword, temporary = true }, // temporary => must change at first login
            ct);
        reset.EnsureSuccessStatusCode();

        return new InvitedAccount(subjectId, temporaryPassword);
    }

    public async Task SetRealmRolesAsync(string subjectId, IReadOnlyCollection<string> roles, CancellationToken ct = default)
    {
        await AuthenticateAsync(ct);
        var path = $"admin/realms/{_options.Realm}/users/{subjectId}/role-mappings/realm";

        // Replace, not merge: the command carries the FULL desired set, so anything currently held
        // and not requested is a removal. Merging would make role removal impossible through this
        // path and quietly turn every assignment into a grant-only operation.
        var current = await _http.GetFromJsonAsync<List<RealmRole>>(path, ct) ?? new List<RealmRole>();
        var desired = new HashSet<string>(roles, StringComparer.Ordinal);

        var toRemove = current.Where(r => !desired.Contains(r.Name)).ToArray();
        if (toRemove.Length > 0)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, path) { Content = JsonContent.Create(toRemove) };
            (await _http.SendAsync(request, ct)).EnsureSuccessStatusCode();
        }

        var available = await _http.GetFromJsonAsync<List<RealmRole>>($"{path}/available", ct) ?? new List<RealmRole>();
        var toAdd = available.Where(r => desired.Contains(r.Name)).ToArray();
        if (toAdd.Length > 0)
            (await _http.PostAsJsonAsync(path, toAdd, ct)).EnsureSuccessStatusCode();
    }

    public async Task SignOutEverywhereAsync(string subjectId, CancellationToken ct = default)
    {
        await AuthenticateAsync(ct);
        var res = await _http.PostAsync($"admin/realms/{_options.Realm}/users/{subjectId}/logout", null, ct);
        res.EnsureSuccessStatusCode();
    }

    public async Task DisableUserAsync(string subjectId, CancellationToken ct = default)
    {
        await AuthenticateAsync(ct);
        // Disable, never delete: deleting strands the member row forever (DEF-029).
        var res = await _http.PutAsJsonAsync($"admin/realms/{_options.Realm}/users/{subjectId}", new { enabled = false }, ct);
        res.EnsureSuccessStatusCode();
    }

    // SC-017 — the fifth write, and the exact inverse of the one above. The sweep disables the
    // Keycloak account, so reactivation has to re-enable it or the member returns Active with a dead
    // login (FR-162, AC-111).
    public async Task EnableUserAsync(string subjectId, CancellationToken ct = default)
    {
        await AuthenticateAsync(ct);
        var res = await _http.PutAsJsonAsync($"admin/realms/{_options.Realm}/users/{subjectId}", new { enabled = true }, ct);
        res.EnsureSuccessStatusCode();
    }

    // SC-011 — the one READ on this port. Feeds the DEC-046 reconciliation, which has to find the
    // accounts that have NO committee_members row (DEF-065).
    public async Task<IReadOnlyList<IdentityAccount>> ListUsersAsync(CancellationToken ct = default)
    {
        await AuthenticateAsync(ct);

        // PAGED RATHER THAN CAPPED. Keycloak's own default is max=100 and it applies silently, so a
        // single call is a listing that becomes wrong at 101 accounts while still reporting success —
        // and a reconciliation that cannot see an account is exactly the failure it exists to fix.
        // The loop removes the ceiling instead of documenting one.
        var users = new List<KeycloakUser>();
        for (var first = 0; ; first += PageSize)
        {
            var page = await _http.GetFromJsonAsync<List<KeycloakUser>>(
                $"admin/realms/{_options.Realm}/users?first={first}&max={PageSize}", ct) ?? new List<KeycloakUser>();
            users.AddRange(page);
            if (page.Count < PageSize)
                break;
        }

        // ⚠ ONE ROLE CALL PER USER, AND THE OBVIOUS OPTIMISATION IS REFUSED. Listing by role instead
        // (GET /roles/{name}/users, six calls regardless of user count) returns **403** under the
        // {manage-users} grant — measured against a real Keycloak, not read from documentation. So
        // this is not a choice between shapes: the per-user read is the one the minimal grant allows,
        // and the alternative would have failed in production only. The committee is capped at ~20
        // users and this runs once, by hand, so the call count is not the constraint.
        var accounts = new List<IdentityAccount>(users.Count);
        foreach (var user in users)
        {
            var roles = await _http.GetFromJsonAsync<List<RealmRole>>(
                $"admin/realms/{_options.Realm}/users/{user.Id}/role-mappings/realm", ct) ?? new List<RealmRole>();

            accounts.Add(new IdentityAccount(
                user.Id,
                user.Email ?? string.Empty,
                // Rejoins what CreateUserAsync's SplitName took apart. An account seeded with neither
                // name falls back to its username, because a member row with an EMPTY display name is
                // a roster line nobody can identify.
                JoinName(user.FirstName, user.LastName) is { Length: > 0 } name ? name : user.Username ?? user.Id,
                user.Enabled,
                roles.Select(r => r.Name).ToArray()));
        }

        return accounts;
    }

    // Client-credentials against the service account. Fetched per operation rather than cached: these
    // are rare administrative calls, and a cached token that expires mid-life is the failure mode the
    // ECR credential helper was introduced to avoid on the deployment side.
    private async Task AuthenticateAsync(CancellationToken ct)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
        });

        var res = await _http.PostAsync($"realms/{_options.Realm}/protocol/openid-connect/token", form, ct);
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var token = doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Keycloak returned no access_token for the service account.");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    // Cryptographically random, and shown once. Never persisted, never logged (AC-088).
    private static string GeneratePassword() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(18)).Replace("+", "-").Replace("/", "_");

    private static (string First, string Last) SplitName(string fullName)
    {
        var parts = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : (fullName.Trim(), string.Empty);
    }

    private static string JoinName(string? first, string? last) =>
        string.Join(' ', new[] { first, last }.Where(p => !string.IsNullOrWhiteSpace(p))).Trim();

    /// <summary>How many accounts one listing call asks for. Keycloak's own default is 100.</summary>
    private const int PageSize = 100;

    private sealed record RealmRole(string Id, string Name);

    // Only the fields the reconciliation reads. Keycloak returns far more; deserializing a narrow
    // shape is what keeps this from quietly becoming the general-purpose passthrough ADR-0038 refuses.
    private sealed record KeycloakUser(
        string Id, string? Username, string? Email, string? FirstName, string? LastName, bool Enabled);
}
