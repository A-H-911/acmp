using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Membership;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Membership.Infrastructure.Directory;

// Membership-owned implementation of the IGuestWindowWriter port (DW-025 / DEC-041).
//
// Registered UNCONDITIONALLY, unlike GuestProvisioner beside it: this writes one nullable column and
// needs no identity provider, so making it conditional would make cancelling a meeting fail wherever
// in-app user management is off. Same reasoning as IPrincipalRevalidator.
public sealed class GuestWindowWriter : IGuestWindowWriter
{
    private readonly IMembershipDbContext _db;
    private readonly IAuditSink _audit;

    public GuestWindowWriter(IMembershipDbContext db, IAuditSink audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<int> SetGuestWindowsAsync(
        IReadOnlyCollection<Guid> memberIds, DateTimeOffset expiresAt, CancellationToken ct = default)
    {
        if (memberIds is null || memberIds.Count == 0)
            return 0;

        var ids = memberIds.Distinct().ToArray();

        // `AccessExpiresAt != null` IS THE GUARD, and it is the whole safety property of this port: a
        // window can only be moved for someone who already had one. An ordinary member presenting at
        // the same meeting keeps their null and stays unaffected.
        var affected = await _db.Members
            .Where(m => ids.Contains(m.PublicId) && m.AccessExpiresAt != null)
            .ToListAsync(ct);

        if (affected.Count == 0)
            return 0;

        foreach (var member in affected)
            member.SetAccessWindow(expiresAt);

        await _db.SaveChangesAsync(ct);

        // One audit row per member, not one for the batch: the question a reviewer asks later is
        // "when did THIS person's access change and why", and a batch row cannot answer it.
        foreach (var member in affected)
            await _audit.EmitEnrichedAsync(
                "Membership.GuestAccessWindowChanged", nameof(CommitteeMember), member.PublicId.ToString(), ct: ct);

        return affected.Count;
    }
}
