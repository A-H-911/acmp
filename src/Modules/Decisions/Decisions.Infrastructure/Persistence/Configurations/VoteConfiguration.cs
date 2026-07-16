using System.Text.Json;
using Acmp.Modules.Decisions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Acmp.Modules.Decisions.Infrastructure.Persistence.Configurations;

// Vote aggregate: its own table + an owned vote_ballots child table (FK back to the vote), an owned inline
// QuorumRule (min-present/min-cast columns), and JSON-serialized Options + frozen Tally (small scalar
// snapshots — no child table needed, provider-agnostic so InMemory and SQL Server behave alike). Cross-module
// references (topic/meeting ids) are plain values, no FK (ADR-0001). One ballot per voter is a DB-enforced
// unique index (VoteEntityId, VoterUserId).
public sealed class VoteConfiguration : IEntityTypeConfiguration<Vote>
{
    private static readonly ValueConverter<IReadOnlyList<string>, string> OptionsConverter = new(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        s => JsonSerializer.Deserialize<List<string>>(s, (JsonSerializerOptions?)null) ?? new List<string>());

    private static readonly ValueComparer<IReadOnlyList<string>> OptionsComparer = new(
        (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
        v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
        v => v.ToList());

    private static readonly ValueConverter<VoteTally?, string?> TallyConverter = new(
        v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        s => string.IsNullOrEmpty(s) ? null : JsonSerializer.Deserialize<VoteTally>(s, (JsonSerializerOptions?)null));

    private static readonly ValueComparer<VoteTally?> TallyComparer = new(
        (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
        v => v == null ? 0 : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
        v => v);

    public void Configure(EntityTypeBuilder<Vote> b)
    {
        b.ToTable("votes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasAlternateKey(x => x.PublicId);
        b.Property(x => x.RowVersion).IsRowVersion(); // optimistic concurrency (docs/domain/data-architecture.md §1.5, ADR-0018)

        b.Property(x => x.Key).IsRequired().HasMaxLength(32);
        b.HasIndex(x => x.Key).IsUnique();

        b.Property(x => x.TopicId).IsRequired();
        b.HasIndex(x => x.TopicId);                 // "votes for this topic" access path
        b.Property(x => x.MeetingId);
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.AllowAbstain).IsRequired();

        b.Property(x => x.Options).HasConversion(OptionsConverter).HasColumnName("options_json").IsRequired();
        b.Property(x => x.Options).Metadata.SetValueComparer(OptionsComparer);

        b.Property(x => x.Tally).HasConversion(TallyConverter).HasColumnName("tally_json").HasMaxLength(4000);
        b.Property(x => x.Tally).Metadata.SetValueComparer(TallyComparer);

        b.Property(x => x.ResultSummary).HasMaxLength(2000);
        b.Property(x => x.OpenedAt);
        b.Property(x => x.ClosedAt);
        b.Property(x => x.CounterUserId).HasMaxLength(128);
        b.Property(x => x.CounterName).HasMaxLength(256);
        b.Property(x => x.ChainSealedAt); // D-13 / ADR-0030: per-ballot chain seal timestamp (null = unsealed/legacy)
        b.Property(x => x.CreatedBy).IsRequired().HasMaxLength(128);
        b.Property(x => x.UpdatedBy).HasMaxLength(128);
        b.Ignore(x => x.DomainEvents);

        // Owned inline quorum rule (two int columns on the vote row).
        b.OwnsOne(x => x.QuorumRule, q =>
        {
            q.Property(p => p.MinPresent).HasColumnName("quorum_min_present").IsRequired();
            q.Property(p => p.MinCast).HasColumnName("quorum_min_cast").IsRequired();
        });
        b.Navigation(x => x.QuorumRule).IsRequired();

        b.Navigation(x => x.Ballots).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.OwnsMany(x => x.Ballots, ball =>
        {
            ball.ToTable("vote_ballots");
            ball.WithOwner().HasForeignKey("VoteEntityId");
            ball.HasKey(x => x.Id);
            ball.Property(x => x.Id).ValueGeneratedOnAdd();
            ball.HasIndex(x => x.PublicId).IsUnique();
            ball.Property(x => x.VoterUserId).IsRequired().HasMaxLength(128);
            ball.Property(x => x.VoterName).HasMaxLength(256);
            ball.Property(x => x.Choice).HasMaxLength(64);
            ball.Property(x => x.Recused).IsRequired();
            ball.Property(x => x.CastAt);
            ball.Property(x => x.PreviousHash).HasMaxLength(64); // D-13 / ADR-0030: sealed per-ballot chain links
            ball.Property(x => x.Hash).HasMaxLength(64);
            ball.Ignore(x => x.DomainEvents);
            ball.HasIndex("VoteEntityId", "VoterUserId").IsUnique(); // one ballot per voter (DB backstop)

            // Optional bilingual comment (mirrored en===ar when present) — owned nullable navigation.
            ball.OwnsOne(x => x.Comment, t =>
            {
                t.Property(p => p.En).HasColumnName("comment_en").HasMaxLength(2000);
                t.Property(p => p.Ar).HasColumnName("comment_ar").HasMaxLength(2000);
            });
        });
    }
}
