using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Stream = Acmp.Modules.Membership.Domain.Stream;

namespace Acmp.Modules.Membership.Infrastructure.Persistence;

// Maps ONLY the membership schema (docs/34 section 12: no cross-module tables).
public sealed class MembershipDbContext : ModuleDbContext, IMembershipDbContext
{
    public const string Schema = "membership";

    public MembershipDbContext(DbContextOptions<MembershipDbContext> options, IClock clock, ICurrentUser currentUser)
        : base(options, clock, currentUser)
    {
    }

    public DbSet<CommitteeMember> Members => Set<CommitteeMember>();
    public DbSet<Stream> Streams => Set<Stream>();
    public DbSet<TopicCapabilityGrant> TopicCapabilities => Set<TopicCapabilityGrant>();
    public DbSet<Delegation> Delegations => Set<Delegation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MembershipDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
