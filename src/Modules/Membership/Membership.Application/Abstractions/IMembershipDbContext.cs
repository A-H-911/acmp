using Acmp.Modules.Membership.Domain;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Membership.Application.Abstractions;

// The Application layer talks to persistence through this narrow port; the EF implementation
// lives in Infrastructure.
public interface IMembershipDbContext
{
    DbSet<CommitteeMember> Members { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
