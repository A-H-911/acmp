using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Acmp.Application.Tests.Topics;

// The ITopicReader seam (P15c / FR-115): a topic's key + title snapshot, or null for an unknown id.
// FR-159 widened it with the /session reads — the presenter's topic card and a pre-signed URL for one
// of its materials, SCOPED TO THE TOPIC.
public class TopicReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private static TopicsDbContext Db()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns("kc-sec");
        return new TopicsDbContext(
            new DbContextOptionsBuilder<TopicsDbContext>().UseInMemoryDatabase("topicreader-" + Guid.NewGuid()).Options, clock, user);
    }

    private static TopicReader Reader(TopicsDbContext db, IFileStore? files = null) =>
        new(db, files ?? Substitute.For<IFileStore>(), Options.Create(new StorageOptions()), SeesEverything());

    // FR-163: this suite asserts the READER's projections and its scoping-by-ids, not confidentiality.
    // A permissive scope keeps it measuring what it claims to; the narrowing has its own suites.
    private static ITopicVisibility SeesEverything()
    {
        var v = Substitute.For<ITopicVisibility>();
        v.ResolveAsync(Arg.Any<CancellationToken>())
            .Returns(new TopicVisibilityScope(true, Array.Empty<Guid>()));
        return v;
    }

    private static Topic NewTopic(string key = "TOP-2026-009", string title = "Auth study", string description = "desc") =>
        Topic.Draft(key, title, description, "just",
            TopicType.ResearchDiscovery, TopicUrgency.Normal, TopicSource.CommitteeMember, "kc-sec", "Secretary",
            new[] { "platform" }, Array.Empty<string>(), Array.Empty<string>());

    [Fact]
    public async Task GetSummary_returns_key_and_title_or_null()
    {
        await using var db = Db();
        var topic = NewTopic();
        db.Topics.Add(topic);
        await db.SaveChangesAsync();

        var reader = Reader(db);
        var summary = await reader.GetSummaryAsync(topic.PublicId);
        summary!.Key.Should().Be("TOP-2026-009");
        summary.Title.Should().Be("Auth study");

        (await reader.GetSummaryAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact] // FR-159 — the /session topic card, with its materials in upload order
    public async Task GetBrief_returns_the_card_and_its_materials_or_null()
    {
        await using var db = Db();
        var topic = NewTopic(description: "A proposal to mandate cursor-based pagination.");
        topic.AddAttachment("deck.pdf", "application/pdf", 2048, "key-1", "kc-sec", "Secretary", Now);
        topic.AddAttachment("diagram.svg", "image/svg+xml", 512, "key-2", "kc-sec", "Secretary", Now.AddMinutes(5));
        db.Topics.Add(topic);
        await db.SaveChangesAsync();

        var brief = await Reader(db).GetBriefAsync(topic.PublicId);

        brief!.Summary.Should().Be("A proposal to mandate cursor-based pagination.");
        brief.Materials.Select(m => m.FileName).Should().ContainInOrder("deck.pdf", "diagram.svg");
        brief.Materials[0].ContentType.Should().Be("application/pdf");
        brief.Materials[0].SizeBytes.Should().Be(2048);

        (await Reader(db).GetBriefAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact] // FR-159 / NFR-027 — a short-lived pre-signed URL, never the storage key
    public async Task GetMaterialUrl_presigns_an_attachment_that_is_on_the_topic()
    {
        await using var db = Db();
        var topic = NewTopic();
        var attachment = topic.AddAttachment("deck.pdf", "application/pdf", 2048, "key-1", "kc-sec", "Secretary", Now);
        db.Topics.Add(topic);
        await db.SaveChangesAsync();

        var files = Substitute.For<IFileStore>();
        files.GetPreSignedUrlAsync(Arg.Any<string>(), "key-1", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://storage.example/presigned");

        var url = await Reader(db, files).GetMaterialUrlAsync(topic.PublicId, attachment.PublicId);

        url.Should().Be("https://storage.example/presigned");
    }

    [Fact] // THE SCOPE IS THE LOOKUP: another topic's attachment is not reachable by id
    public async Task GetMaterialUrl_refuses_an_attachment_that_belongs_to_a_different_topic()
    {
        await using var db = Db();
        var mine = NewTopic("TOP-2026-010", "My slot");
        var theirs = NewTopic("TOP-2026-011", "Someone else's topic");
        var theirAttachment = theirs.AddAttachment("secret.pdf", "application/pdf", 99, "key-x", "kc-sec", "Secretary", Now);
        db.Topics.AddRange(mine, theirs);
        await db.SaveChangesAsync();

        var files = Substitute.For<IFileStore>();

        var url = await Reader(db, files).GetMaterialUrlAsync(mine.PublicId, theirAttachment.PublicId);

        url.Should().BeNull();
        // Not merely "no URL returned" — the store was never asked, so nothing was signed at all.
        await files.DidNotReceiveWithAnyArgs().GetPreSignedUrlAsync(default!, default!, default);
    }

    [Fact]
    public async Task GetMaterialUrl_returns_null_for_an_unknown_attachment()
    {
        await using var db = Db();
        var topic = NewTopic();
        db.Topics.Add(topic);
        await db.SaveChangesAsync();

        (await Reader(db).GetMaterialUrlAsync(topic.PublicId, Guid.NewGuid())).Should().BeNull();
    }
}
