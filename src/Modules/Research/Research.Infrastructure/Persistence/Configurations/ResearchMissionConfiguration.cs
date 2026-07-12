using Acmp.Modules.Research.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acmp.Modules.Research.Infrastructure.Persistence.Configurations;

// ResearchMission aggregate → the research_missions table + owned research_findings / research_recommendations
// child tables (FK back to the mission). Bilingual fields are owned LocalizedString value objects (en/ar column
// pair) — Title/Question required, the rest optional (nullable columns). People fields are Keycloak subject
// strings + a name snapshot. Cross-module references (SourceTopicId, a recommendation's LinkedTopicId) are plain
// values, no FK (ADR-0001). The Status + Key indexes back the register filters and the unique display key.
public sealed class ResearchMissionConfiguration : IEntityTypeConfiguration<ResearchMission>
{
    public void Configure(EntityTypeBuilder<ResearchMission> b)
    {
        b.ToTable("research_missions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasAlternateKey(x => x.PublicId);
        b.Property(x => x.RowVersion).IsRowVersion(); // optimistic concurrency (docs/domain/data-architecture.md §1.5, ADR-0018)

        b.Property(x => x.Key).IsRequired().HasMaxLength(32);
        b.HasIndex(x => x.Key).IsUnique();

        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.HasIndex(x => x.Status);

        b.Property(x => x.OwnerUserId).IsRequired().HasMaxLength(128);
        b.Property(x => x.OwnerName).IsRequired().HasMaxLength(256);

        // Deferred references — stored values only (no import, no graph edge in P15a).
        b.Property(x => x.KeystonePackageRef).HasMaxLength(256);
        b.Property(x => x.SourceTopicId);

        b.Property(x => x.CompletedAt);

        b.Property(x => x.CreatedBy).IsRequired().HasMaxLength(128);
        b.Property(x => x.UpdatedBy).HasMaxLength(128);
        b.Ignore(x => x.DomainEvents);

        // Required bilingual fields (en/ar columns NOT NULL).
        b.OwnsOne(x => x.Title, t =>
        {
            t.Property(p => p.En).HasColumnName("title_en").IsRequired().HasMaxLength(512);
            t.Property(p => p.Ar).HasColumnName("title_ar").IsRequired().HasMaxLength(512);
        });
        b.Navigation(x => x.Title).IsRequired();

        b.OwnsOne(x => x.Question, t =>
        {
            t.Property(p => p.En).HasColumnName("question_en").IsRequired().HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("question_ar").IsRequired().HasMaxLength(4000);
        });
        b.Navigation(x => x.Question).IsRequired();

        // Optional bilingual terminal evidence — owned, nullable navigation (columns nullable).
        b.OwnsOne(x => x.CancellationReason, t =>
        {
            t.Property(p => p.En).HasColumnName("cancellation_reason_en").HasMaxLength(4000);
            t.Property(p => p.Ar).HasColumnName("cancellation_reason_ar").HasMaxLength(4000);
        });

        // Owned findings (FR-113) — same pattern as risk_mitigations.
        b.Navigation(x => x.Findings).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.OwnsMany(x => x.Findings, f =>
        {
            f.ToTable("research_findings");
            f.WithOwner().HasForeignKey("MissionEntityId");
            f.HasKey(x => x.Id);
            f.Property(x => x.Id).ValueGeneratedOnAdd();
            f.HasIndex(x => x.PublicId).IsUnique();
            f.Property(x => x.Key).IsRequired().HasMaxLength(32);
            f.Property(x => x.Confidence).HasConversion<int>().IsRequired();
            f.Property(x => x.IsVerified).IsRequired();
            f.Ignore(x => x.DomainEvents);

            f.OwnsOne(x => x.Summary, t =>
            {
                t.Property(p => p.En).HasColumnName("summary_en").IsRequired().HasMaxLength(4000);
                t.Property(p => p.Ar).HasColumnName("summary_ar").IsRequired().HasMaxLength(4000);
            });
            f.Navigation(x => x.Summary).IsRequired();

            f.OwnsOne(x => x.Detail, t =>
            {
                t.Property(p => p.En).HasColumnName("detail_en").HasMaxLength(4000);
                t.Property(p => p.Ar).HasColumnName("detail_ar").HasMaxLength(4000);
            });
        });

        // Owned recommendations (FR-113).
        b.Navigation(x => x.Recommendations).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.OwnsMany(x => x.Recommendations, r =>
        {
            r.ToTable("research_recommendations");
            r.WithOwner().HasForeignKey("MissionEntityId");
            r.HasKey(x => x.Id);
            r.Property(x => x.Id).ValueGeneratedOnAdd();
            r.HasIndex(x => x.PublicId).IsUnique();
            r.Property(x => x.Key).IsRequired().HasMaxLength(32);
            r.Property(x => x.Priority).HasConversion<int>().IsRequired();
            r.Property(x => x.Status).HasConversion<int>().IsRequired();
            r.Property(x => x.LinkedTopicId); // stored only — no FK (P15c)
            r.Ignore(x => x.DomainEvents);

            r.OwnsOne(x => x.Statement, t =>
            {
                t.Property(p => p.En).HasColumnName("statement_en").IsRequired().HasMaxLength(4000);
                t.Property(p => p.Ar).HasColumnName("statement_ar").IsRequired().HasMaxLength(4000);
            });
            r.Navigation(x => x.Statement).IsRequired();

            r.OwnsOne(x => x.Rationale, t =>
            {
                t.Property(p => p.En).HasColumnName("rationale_en").HasMaxLength(4000);
                t.Property(p => p.Ar).HasColumnName("rationale_ar").HasMaxLength(4000);
            });
        });
    }
}

internal sealed class ResearchKeyCounterConfiguration : IEntityTypeConfiguration<ResearchKeyCounter>
{
    public void Configure(EntityTypeBuilder<ResearchKeyCounter> b)
    {
        b.ToTable("research_key_counters");
        b.HasKey(x => new { x.Prefix, x.Year });
        b.Property(x => x.Prefix).HasMaxLength(8);
        b.Property(x => x.Next).IsRequired();
    }
}
