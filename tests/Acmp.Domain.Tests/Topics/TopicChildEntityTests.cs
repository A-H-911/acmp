using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using FluentAssertions;

namespace Acmp.Domain.Tests.Topics;

/// <summary>
/// Covers the uncovered branches of TopicComment and TopicAttachment:
/// - TopicComment: successful construction (body/author attribution, body trimming).
///   The existing TopicTests only exercises the empty-body guard path, so the
///   TopicComment constructor body is not reached there.
/// - TopicAttachment: all metadata fields beyond FileName/SizeBytes (ContentType,
///   StorageKey, UploadedBySub, UploadedByName, UploadedAt) and field trimming.
/// </summary>
public sealed class TopicChildEntityTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 10, 11, 0, 0, TimeSpan.Zero);
    private const string ActorSub = "kc-khalid";
    private const string ActorName = "Khalid A.";
    private const string OwnerSub = "kc-omar";
    private const string OwnerName = "Omar H.";
    private static readonly Guid OwnerId = Guid.NewGuid();

    // ---- helpers ----

    /// <summary>
    /// A minimal Draft topic — AddComment has no EnsureMutable guard,
    /// so Draft status is sufficient to exercise the comment constructor.
    /// </summary>
    private static Topic AnyDraft() => Topic.Draft(
        "TOP-2026-100",
        "Child entity tests",
        "Validates comment and attachment metadata.",
        "Coverage for TopicComment and TopicAttachment constructors.",
        TopicType.ArchitectureDecision,
        TopicUrgency.Normal,
        TopicSource.CommitteeMember,
        OwnerSub,
        OwnerName,
        new[] { "platform" },
        new[] { "Core API" },
        Array.Empty<string>());

    /// <summary>
    /// An Accepted topic — AddAttachment calls EnsureMutable(), so the topic
    /// must be in a mutable status (not Decided/Closed/Converted).
    /// </summary>
    private static Topic AnAccepted()
    {
        var t = AnyDraft();
        t.Submit(Now);
        t.BeginTriage(ActorSub, ActorName, Now);
        t.Accept(OwnerId, OwnerName, ActorSub, ActorName, Now);
        return t;
    }

    // ---- TopicComment ----

    [Fact]
    public void AddComment_sets_body_and_full_author_attribution()
    {
        // Arrange
        var t = AnyDraft();

        // Act
        t.AddComment("Initial assessment complete.", OwnerSub, OwnerName, Now);

        // Assert
        var comment = t.Comments.Should().ContainSingle().Subject;
        comment.Body.Should().Be("Initial assessment complete.");
        comment.AuthorSub.Should().Be(OwnerSub);
        comment.AuthorName.Should().Be(OwnerName);
        comment.PostedAt.Should().Be(Now);
    }

    [Fact]
    public void AddComment_trims_leading_and_trailing_whitespace_from_body()
    {
        // Arrange
        var t = AnyDraft();

        // Act
        t.AddComment("  Needs more context.  ", OwnerSub, OwnerName, Now);

        // Assert
        t.Comments.Should().ContainSingle(c => c.Body == "Needs more context.");
    }

    [Fact]
    public void AddComment_multiple_comments_accumulate_independently()
    {
        // Arrange
        var t = AnyDraft();
        var later = Now.AddHours(1);

        // Act
        t.AddComment("First comment.", OwnerSub, OwnerName, Now);
        t.AddComment("Second comment.", ActorSub, ActorName, later);

        // Assert
        t.Comments.Should().HaveCount(2);
        t.Comments.Should().Contain(c => c.Body == "First comment." && c.AuthorSub == OwnerSub);
        t.Comments.Should().Contain(c => c.Body == "Second comment." && c.AuthorSub == ActorSub);
    }

    // ---- TopicAttachment ----

    [Fact]
    public void AddAttachment_records_all_metadata_fields()
    {
        // Arrange
        var t = AnAccepted();
        const string fileName = "evaluation.pdf";
        const string contentType = "application/pdf";
        const long sizeBytes = 2_048_000;
        const string storageKey = "topics/2026/099/evaluation.pdf";

        // Act
        t.AddAttachment(fileName, contentType, sizeBytes, storageKey, OwnerSub, OwnerName, Now);

        // Assert
        var att = t.Attachments.Should().ContainSingle().Subject;
        att.FileName.Should().Be(fileName);
        att.ContentType.Should().Be(contentType);
        att.SizeBytes.Should().Be(sizeBytes);
        att.StorageKey.Should().Be(storageKey);
        att.UploadedBySub.Should().Be(OwnerSub);
        att.UploadedByName.Should().Be(OwnerName);
        att.UploadedAt.Should().Be(Now);
    }

    [Fact]
    public void AddAttachment_trims_string_fields()
    {
        // Arrange
        var t = AnAccepted();

        // Act
        t.AddAttachment("  report.docx  ", "  application/vnd.openxmlformats  ", 1000,
            "  topics/key  ", "  kc-omar  ", "  Omar H.  ", Now);

        // Assert
        var att = t.Attachments.Should().ContainSingle().Subject;
        att.FileName.Should().Be("report.docx");
        att.ContentType.Should().Be("application/vnd.openxmlformats");
        att.StorageKey.Should().Be("topics/key");
        att.UploadedBySub.Should().Be("kc-omar");
        att.UploadedByName.Should().Be("Omar H.");
    }

    [Fact]
    public void AddAttachment_returns_the_new_attachment_instance()
    {
        // Arrange
        var t = AnAccepted();

        // Act
        var returned = t.AddAttachment("arch.png", "image/png", 300_000, "topics/arch.png", OwnerSub, OwnerName, Now);

        // Assert — the returned reference is the same object now in the collection
        returned.Should().BeSameAs(t.Attachments.Single());
    }

    [Fact]
    public void Multiple_attachments_accumulate_independently()
    {
        // Arrange
        var t = AnAccepted();

        // Act
        t.AddAttachment("a.pdf", "application/pdf", 100, "key/a", OwnerSub, OwnerName, Now);
        t.AddAttachment("b.png", "image/png", 200, "key/b", ActorSub, ActorName, Now.AddMinutes(5));

        // Assert
        t.Attachments.Should().HaveCount(2);
        t.Attachments.Should().Contain(a => a.FileName == "a.pdf" && a.StorageKey == "key/a");
        t.Attachments.Should().Contain(a => a.FileName == "b.png" && a.StorageKey == "key/b");
    }
}
