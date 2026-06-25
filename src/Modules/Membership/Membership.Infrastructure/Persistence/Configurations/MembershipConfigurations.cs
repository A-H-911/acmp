using Acmp.Modules.Membership.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stream = Acmp.Modules.Membership.Domain.Stream;

namespace Acmp.Modules.Membership.Infrastructure.Persistence.Configurations;

public sealed class StreamConfiguration : IEntityTypeConfiguration<Stream>
{
    public void Configure(EntityTypeBuilder<Stream> b)
    {
        b.ToTable("streams");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasAlternateKey(x => x.PublicId);
        b.Property(x => x.Code).IsRequired().HasMaxLength(64);
        b.HasIndex(x => x.Code).IsUnique();
        b.OwnsOne(x => x.Name, n =>
        {
            n.Property(p => p.En).HasColumnName("name_en").IsRequired().HasMaxLength(128);
            n.Property(p => p.Ar).HasColumnName("name_ar").IsRequired().HasMaxLength(128);
        });
        b.Property(x => x.CreatedBy).IsRequired().HasMaxLength(128);
        b.Property(x => x.UpdatedBy).HasMaxLength(128);
        b.Ignore(x => x.DomainEvents);
    }
}

public sealed class TopicCapabilityGrantConfiguration : IEntityTypeConfiguration<TopicCapabilityGrant>
{
    public void Configure(EntityTypeBuilder<TopicCapabilityGrant> b)
    {
        b.ToTable("topic_capability_grants");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasAlternateKey(x => x.PublicId);
        b.Property(x => x.TopicId).IsRequired();
        b.Property(x => x.Capability).HasConversion<int>().IsRequired();
        b.Property(x => x.CreatedBy).IsRequired().HasMaxLength(128);
        b.Property(x => x.UpdatedBy).HasMaxLength(128);
        b.HasIndex(x => new { x.CommitteeMemberId, x.TopicId });
        b.HasOne<CommitteeMember>().WithMany().HasForeignKey(x => x.CommitteeMemberId);
        b.Ignore(x => x.DomainEvents);
    }
}

public sealed class DelegationConfiguration : IEntityTypeConfiguration<Delegation>
{
    public void Configure(EntityTypeBuilder<Delegation> b)
    {
        b.ToTable("delegations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasAlternateKey(x => x.PublicId);
        b.Property(x => x.Capability).IsRequired().HasMaxLength(128);
        b.Property(x => x.CreatedBy).IsRequired().HasMaxLength(128);
        b.Property(x => x.UpdatedBy).HasMaxLength(128);
        b.HasIndex(x => new { x.DelegateMemberId, x.ValidTo });
        b.HasOne<CommitteeMember>().WithMany().HasForeignKey(x => x.DelegatorMemberId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CommitteeMember>().WithMany().HasForeignKey(x => x.DelegateMemberId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.DomainEvents);
    }
}
