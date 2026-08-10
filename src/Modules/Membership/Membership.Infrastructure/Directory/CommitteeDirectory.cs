using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Shared.Contracts.Membership;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Membership.Infrastructure.Directory;

// Membership-owned implementation of the shared ICommitteeDirectory port (ADR-0001): resolves the
// committee roster for other modules (e.g. the Meetings notification fan-out) without exposing
// Membership tables. ACTIVE members only — disabled members are access-blocked (AC-058) and receive
// no notifications. UserId = the Keycloak subject (matches NotificationMessage.RecipientUserId).
public sealed class CommitteeDirectory : ICommitteeDirectory
{
    private readonly IMembershipDbContext _db;

    public CommitteeDirectory(IMembershipDbContext db) => _db = db;

    public async Task<IReadOnlyCollection<CommitteeRecipient>> GetActiveMembersAsync(CancellationToken ct = default) =>
        await _db.Members.AsNoTracking()
            .Where(m => m.Status == MembershipStatus.Active)
            .OrderBy(m => m.FullName)
            .Select(m => new CommitteeRecipient(m.KeycloakUserId, m.FullName))
            .ToListAsync(ct);

    // Role is a claims-derived cache on CommitteeMember (refreshed each login), so this is "who currently
    // holds the role, per the roster". The role-name maps 1:1 to CommitteeRole via nameof; an unknown name
    // yields no recipients rather than throwing.
    public async Task<IReadOnlyCollection<CommitteeRecipient>> GetActiveMembersInRoleAsync(string role, CancellationToken ct = default)
    {
        if (!Enum.TryParse<CommitteeRole>(role, ignoreCase: false, out var parsed))
            return Array.Empty<CommitteeRecipient>();

        return await _db.Members.AsNoTracking()
            .Where(m => m.Status == MembershipStatus.Active && m.Role == parsed)
            .OrderBy(m => m.FullName)
            .Select(m => new CommitteeRecipient(m.KeycloakUserId, m.FullName))
            .ToListAsync(ct);
    }

    // NO Status filter, and that is the point — see the contract's comment. The audit register resolves
    // actors through this, and a disabled member's past actions must still read as a person's name.
    public async Task<IReadOnlyDictionary<string, string>> ResolveDisplayNamesAsync(
        IReadOnlyCollection<string> userIds, CancellationToken ct = default)
    {
        if (userIds is null || userIds.Count == 0)
            return new Dictionary<string, string>();

        // Distinct so a page of audit rows by one actor issues one predicate, not one per row.
        var ids = userIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<string, string>();

        var rows = await _db.Members.AsNoTracking()
            .Where(m => ids.Contains(m.KeycloakUserId))
            .Select(m => new { m.KeycloakUserId, m.FullName })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.KeycloakUserId, r => r.FullName);
    }
}
