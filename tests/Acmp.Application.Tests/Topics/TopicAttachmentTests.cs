using System.Text;
using Acmp.Modules.Topics.Application;
using Acmp.Modules.Topics.Application.Features.AttachFileToTopic;
using Acmp.Modules.Topics.Application.Features.SubmitTopic;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Acmp.Application.Tests.Topics;

// AC-049 (size/MIME validation) and AC-050 (valid upload → MinIO via IFileStore + metadata + audit).
public class TopicAttachmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 15, 9, 0, 0, TimeSpan.Zero);
    private static readonly IOptions<TopicAttachmentOptions> Options = Microsoft.Extensions.Options.Options.Create(new TopicAttachmentOptions());

    // Content sniffing (C-FILE-01) is covered by MimeFileContentInspectorTests; here it is a pass-through.
    private static readonly IFileContentInspector PassInspector = new PassThroughInspector();

    private sealed class PassThroughInspector : IFileContentInspector
    {
        public bool ContentMatchesDeclared(Stream content, string declaredContentType) => true;
    }

    [Fact]
    public void Validator_rejects_oversize_and_disallowed_types()
    {
        var v = new AttachFileToTopicValidator(Options);
        var ok = new AttachFileToTopicCommand(Guid.NewGuid(), "a.pdf", "application/pdf", 1024, Stream.Null);
        v.Validate(ok).IsValid.Should().BeTrue();

        v.Validate(ok with { SizeBytes = 60L * 1024 * 1024 }).IsValid.Should().BeFalse();   // > 50 MB
        v.Validate(ok with { ContentType = "application/x-msdownload" }).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Valid_upload_stores_metadata_and_emits_audit()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns("kc-omar");
        user.DisplayName.Returns("Omar H.");

        await using var db = new TopicsDbContext(
            new DbContextOptionsBuilder<TopicsDbContext>().UseInMemoryDatabase("attach-" + Guid.NewGuid()).Options,
            clock, user);

        // Submit a topic as the uploader so the submitter bypass authorizes the attach.
        var submit = await new SubmitTopicHandler(db, new TopicKeyGenerator(db), user, clock, Substitute.For<IAuditSink>())
            .Handle(new SubmitTopicCommand("T", "D", "J", TopicType.ArchitectureDecision, TopicUrgency.Normal,
                TopicSource.CommitteeMember, new[] { "identity" }, Array.Empty<string>(), Array.Empty<string>()),
                CancellationToken.None);

        var files = Substitute.For<IFileStore>();
        files.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult($"{ci.ArgAt<string>(0)}/{ci.ArgAt<string>(1)}"));
        var audit = Substitute.For<IAuditSink>();

        var content = new MemoryStream(Encoding.UTF8.GetBytes("pdf-bytes"));
        var dto = await new AttachFileToTopicHandler(db, Substitute.For<IResourceAuthorizer>(), files, user, clock, audit, PassInspector)
            .Handle(new AttachFileToTopicCommand(submit.Id, "eval.pdf", "application/pdf", 9, content), CancellationToken.None);

        dto.FileName.Should().Be("eval.pdf");
        var stored = await db.Topics.Include(t => t.Attachments).SingleAsync();
        stored.Attachments.Should().ContainSingle(a => a.FileName == "eval.pdf" && a.StorageKey.StartsWith("acmp-topics/"));
        await files.Received(1).UploadAsync("acmp-topics", Arg.Any<string>(), Arg.Any<Stream>(), "application/pdf", Arg.Any<CancellationToken>());
        await audit.Received(1).EmitEnrichedAsync("Topics.DocumentAttached", "Topic", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory] // C-FILE-01: the object key is server-derived (guid + content-type extension), never the raw filename.
    [InlineData("application/pdf", ".pdf")]
    [InlineData("image/png", ".png")]
    [InlineData("image/jpeg", ".jpg")]
    [InlineData("image/svg+xml", ".svg")]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx")]
    public async Task Object_key_uses_a_server_derived_extension(string contentType, string ext)
    {
        var (db, topicId, user, clock, files, audit) = await SetupTopicAsync();
        var dto = await new AttachFileToTopicHandler(db, Substitute.For<IResourceAuthorizer>(), files, user, clock, audit, PassInspector)
            .Handle(new AttachFileToTopicCommand(topicId, "orig name (1).x", contentType, 9, new MemoryStream(new byte[] { 1 })), CancellationToken.None);

        var stored = await db.Topics.Include(t => t.Attachments).SingleAsync();
        stored.Attachments.Single().StorageKey.Should().EndWith(ext);
        stored.Attachments.Single().StorageKey.Should().NotContain("orig name"); // raw client filename never in the key
    }

    [Fact] // C-FILE-01: content whose bytes don't match the declared type is rejected before any store
    public async Task Mismatched_content_is_rejected_pre_store()
    {
        var (db, topicId, user, clock, files, audit) = await SetupTopicAsync();
        var reject = Substitute.For<IFileContentInspector>();
        reject.ContentMatchesDeclared(Arg.Any<Stream>(), Arg.Any<string>()).Returns(false);

        var handler = new AttachFileToTopicHandler(db, Substitute.For<IResourceAuthorizer>(), files, user, clock, audit, reject);
        await FluentActions.Awaiting(() => handler.Handle(
                new AttachFileToTopicCommand(topicId, "a.pdf", "application/pdf", 9, new MemoryStream(new byte[] { 1 })), CancellationToken.None))
            .Should().ThrowAsync<FluentValidation.ValidationException>();
        await files.DidNotReceive().UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // Seeds an InMemory Topics store with one topic owned by the current user (submitter bypass authorizes attach).
    private static async Task<(TopicsDbContext Db, Guid TopicId, ICurrentUser User, IClock Clock, IFileStore Files, IAuditSink Audit)> SetupTopicAsync()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns("kc-omar");
        user.DisplayName.Returns("Omar H.");

        var db = new TopicsDbContext(
            new DbContextOptionsBuilder<TopicsDbContext>().UseInMemoryDatabase("attach-" + Guid.NewGuid()).Options, clock, user);
        var submit = await new SubmitTopicHandler(db, new TopicKeyGenerator(db), user, clock, Substitute.For<IAuditSink>())
            .Handle(new SubmitTopicCommand("T", "D", "J", TopicType.ArchitectureDecision, TopicUrgency.Normal,
                TopicSource.CommitteeMember, new[] { "identity" }, Array.Empty<string>(), Array.Empty<string>()), CancellationToken.None);

        var files = Substitute.For<IFileStore>();
        files.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult($"{ci.ArgAt<string>(0)}/{ci.ArgAt<string>(1)}"));
        return (db, submit.Id, user, clock, files, Substitute.For<IAuditSink>());
    }
}
