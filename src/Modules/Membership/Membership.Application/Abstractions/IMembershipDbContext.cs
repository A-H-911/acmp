using Acmp.Modules.Membership.Domain;
using Microsoft.EntityFrameworkCore;
using Stream = Acmp.Modules.Membership.Domain.Stream;

namespace Acmp.Modules.Membership.Application.Abstractions;

// The Application layer talks to persistence through this narrow port; the EF implementation
// lives in Infrastructure.
public interface IMembershipDbContext
{
    DbSet<CommitteeMember> Members { get; }
    DbSet<Stream> Streams { get; }
    DbSet<TopicCapabilityGrant> TopicCapabilities { get; }
    DbSet<Delegation> Delegations { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
