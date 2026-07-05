using Acmp.Modules.Membership.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acmp.Modules.Membership.Infrastructure.Persistence.Configurations;

public sealed class CommitteeMemberConfiguration : IEntityTypeConfiguration<CommitteeMember>
{
    public void Configure(EntityTypeBuilder<CommitteeMember> b)
    {
        b.ToTable("committee_members");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasAlternateKey(x => x.PublicId);
        b.Property(x => x.RowVersion).IsRowVersion(); // optimistic concurrency (docs/domain/data-architecture.md §1.5, ADR-0018)
        b.Property(x => x.KeycloakUserId).IsRequired().HasMaxLength(128);
        b.Property(x => x.FullName).IsRequired().HasMaxLength(256);
        b.Property(x => x.Email).IsRequired().HasMaxLength(256);
        b.Property(x => x.Role).HasConversion<int>().IsRequired();
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.IsVotingEligible).IsRequired();
        b.Property(x => x.CreatedBy).IsRequired().HasMaxLength(128);
        b.Property(x => x.UpdatedBy).HasMaxLength(128);
        b.HasIndex(x => x.KeycloakUserId).IsUnique();
        // Email uniqueness applies only to real emails: Keycloak users may have no email (e.g. the
        // bootstrap admin), so JIT provisions an empty email — a plain unique index would 409 the second
        // emailless member. KeycloakUserId is the stable identity; email is unique only WHERE present.
        b.HasIndex(x => x.Email).IsUnique().HasFilter("[Email] <> ''");
        b.Ignore(x => x.IsActive); // derived from Status
        b.Ignore(x => x.DomainEvents);

        // Stream assignments are an owned collection accessed through the _streams backing field.
        b.Navigation(x => x.Streams).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.OwnsMany(x => x.Streams, sa =>
        {
            sa.ToTable("member_streams");
            sa.WithOwner().HasForeignKey(s => s.CommitteeMemberId);
            sa.HasKey(s => new { s.CommitteeMemberId, s.StreamId });
            sa.Property(s => s.StreamId);
        });
    }
}
