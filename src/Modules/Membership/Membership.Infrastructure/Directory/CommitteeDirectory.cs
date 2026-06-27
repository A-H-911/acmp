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
}
