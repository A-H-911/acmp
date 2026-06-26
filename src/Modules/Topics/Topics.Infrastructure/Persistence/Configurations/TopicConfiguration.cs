using System.Text.Json;
using Acmp.Modules.Topics.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Acmp.Modules.Topics.Infrastructure.Persistence.Configurations;

public sealed class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    // Streams/systems/tags are short string lists — stored as a single JSON column each (provider-portable;
    // queried in memory at committee scale). A value comparer lets EF track edits to the list contents.
    private static readonly ValueConverter<List<string>, string> ListConverter = new(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

    private static readonly ValueComparer<List<string>> ListComparer = new(
        (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
        v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
        v => v.ToList());

    public void Configure(EntityTypeBuilder<Topic> b)
    {
        b.ToTable("topics");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.HasAlternateKey(x => x.PublicId);
        b.Property(x => x.Key).IsRequired().HasMaxLength(32);
        b.HasIndex(x => x.Key).IsUnique();

        b.Property(x => x.Title).IsRequired().HasMaxLength(200);
        b.Property(x => x.Description).IsRequired();
        b.Property(x => x.Justification).HasDefaultValue(string.Empty);
        b.Property(x => x.Type).HasConversion<int>().IsRequired();
        b.Property(x => x.Urgency).HasConversion<int>().IsRequired();
        b.Property(x => x.Scope).HasConversion<int>().IsRequired();
        b.Property(x => x.Source).HasConversion<int>().IsRequired();
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.Priority);
        b.Property(x => x.SubmittedBySub).IsRequired().HasMaxLength(128);
        b.Property(x => x.SubmittedByName).IsRequired().HasMaxLength(256);
        b.Property(x => x.OwnerId);
        b.Property(x => x.OwnerName).HasMaxLength(256);
        b.Property(x => x.RevisitOn);
        b.Property(x => x.CreatedBy).IsRequired().HasMaxLength(128);
        b.Property(x => x.UpdatedBy).HasMaxLength(128);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.OwnerId);

        // String collections via JSON columns (backing fields).
        b.Property<List<string>>("_streams").HasColumnName("streams").HasConversion(ListConverter, ListComparer).IsRequired();
        b.Property<List<string>>("_systems").HasColumnName("systems").HasConversion(ListConverter, ListComparer).IsRequired();
        b.Property<List<string>>("_tags").HasColumnName("tags").HasConversion(ListConverter, ListComparer).IsRequired();
        b.Ignore(x => x.AffectedStreams);
        b.Ignore(x => x.Systems);
        b.Ignore(x => x.Tags);
        b.Ignore(x => x.DomainEvents);

        // Child collections owned by the aggregate (their own tables; FK back to the topic).
        b.Navigation(x => x.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.OwnsMany(x => x.Attachments, a =>
        {
            a.ToTable("topic_attachments");
            a.WithOwner().HasForeignKey("TopicEntityId");
            a.HasKey(x => x.Id);
            a.Property(x => x.Id).ValueGeneratedOnAdd();
            a.HasIndex(x => x.PublicId).IsUnique();
            a.Property(x => x.FileName).IsRequired().HasMaxLength(256);
            a.Property(x => x.ContentType).IsRequired().HasMaxLength(128);
            a.Property(x => x.StorageKey).IsRequired().HasMaxLength(512);
            a.Property(x => x.UploadedBySub).IsRequired().HasMaxLength(128);
            a.Property(x => x.UploadedByName).IsRequired().HasMaxLength(256);
            a.Ignore(x => x.DomainEvents);
        });

        b.Navigation(x => x.Comments).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.OwnsMany(x => x.Comments, c =>
        {
            c.ToTable("topic_comments");
            c.WithOwner().HasForeignKey("TopicEntityId");
            c.HasKey(x => x.Id);
            c.Property(x => x.Id).ValueGeneratedOnAdd();
            c.HasIndex(x => x.PublicId).IsUnique();
            c.Property(x => x.Body).IsRequired();
            c.Property(x => x.AuthorSub).IsRequired().HasMaxLength(128);
            c.Property(x => x.AuthorName).IsRequired().HasMaxLength(256);
            c.Ignore(x => x.DomainEvents);
        });

        b.Navigation(x => x.History).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.OwnsMany(x => x.History, h =>
        {
            h.ToTable("topic_status_events");
            h.WithOwner().HasForeignKey("TopicEntityId");
            h.HasKey(x => x.Id);
            h.Property(x => x.Id).ValueGeneratedOnAdd();
            h.HasIndex(x => x.PublicId).IsUnique();
            h.Property(x => x.FromStatus).HasConversion<int>();
            h.Property(x => x.ToStatus).HasConversion<int>();
            h.Property(x => x.Reason).HasMaxLength(2000);
            h.Property(x => x.ActorSub).IsRequired().HasMaxLength(128);
            h.Property(x => x.ActorName).IsRequired().HasMaxLength(256);
            h.Ignore(x => x.DomainEvents);
        });
    }
}

internal sealed class TopicKeyCounterConfiguration : IEntityTypeConfiguration<TopicKeyCounter>
{
    public void Configure(EntityTypeBuilder<TopicKeyCounter> b)
    {
        b.ToTable("topic_key_counters");
        b.HasKey(x => x.Year);
        b.Property(x => x.Year).ValueGeneratedNever();
        b.Property(x => x.Next).IsRequired();
    }
}
