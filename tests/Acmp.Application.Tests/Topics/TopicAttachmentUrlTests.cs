using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Features.GetTopicAttachmentUrl;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Acmp.Application.Tests.Topics;

// WBS-40.12 / AC-163 (DEC-190): the committee download handler, run over the REAL TopicReader (the reader the
// guest path uses) with a capturing file store, so the visibility scope, the expiry actually handed to the
// store and the audit row are all measured on the path production takes. Restricted-topic narrowing is
// driven through the visibility scope the reader resolves, as FR-163's own suites do.
public class TopicAttachmentUrlTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 9, 0, 0, TimeSpan.Zero);

    private sealed class Rig
    {
        public TopicsDbContext Db { get; }
        public IFileStore Files { get; } = Substitute.For<IFileStore>();
        public IAuditSink Audit { get; } = Substitute.For<IAuditSink>();
        public TimeSpan? Expiry { get; private set; }
        public string? DownloadName { get; private set; }

        public Rig()
        {
            var clock = Substitute.For<IClock>();
            clock.UtcNow.Returns(Now);
            var user = Substitute.For<ICurrentUser>();
            user.UserId.Returns("kc-sec");
            Db = new TopicsDbContext(new DbContextOptionsBuilder<TopicsDbContext>()
                .UseInMemoryDatabase("attach-url-" + Guid.NewGuid()).Options, clock, user);
            // AC-164: the committee path mints a DOWNLOAD link, carrying the original file name.
            Files.GetDownloadUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                .Returns(ci => { DownloadName = ci.ArgAt<string>(2); Expiry = ci.ArgAt<TimeSpan>(3); return "https://storage.example/presigned"; });
        }

        public Task<string?> RunAsync(Guid topicId, Guid attachmentId, TopicVisibilityScope scope)
        {
            var visibility = Substitute.For<ITopicVisibility>();
            visibility.ResolveAsync(Arg.Any<CancellationToken>()).Returns(scope);
            var reader = new TopicReader(Db, Files, Options.Create(new StorageOptions()), visibility);
            return new GetTopicAttachmentUrlHandler(reader, Audit).Handle(new GetTopicAttachmentUrlQuery(topicId, attachmentId), default);
        }
    }

    private static readonly TopicVisibilityScope Member = new(false, Array.Empty<Guid>());

    private static async Task<(Topic Topic, TopicAttachment Attachment)> SeedAsync(Rig rig, bool restricted = false, string key = "TOP-2026-040")
    {
        var topic = Topic.Draft(key, "Adopt Keycloak", "desc", "just", TopicType.ArchitectureDecision, TopicUrgency.Normal,
            TopicSource.CommitteeMember, "kc-sub", "Submitter", new[] { "core" }, Array.Empty<string>(), Array.Empty<string>());
        var attachment = topic.AddAttachment("deck.pdf", "application/pdf", 2048, $"key-{key}", "kc-sub", "Submitter", Now);
        if (restricted) topic.Restrict("kc-sec", "Secretary", Now);
        rig.Db.Topics.Add(topic);
        await rig.Db.SaveChangesAsync();
        return (topic, attachment);
    }

    [Fact]
    public async Task A_reader_of_the_topic_gets_a_url_under_an_hour_and_one_audit_row_naming_the_attachment()
    {
        var rig = new Rig();
        var (topic, attachment) = await SeedAsync(rig);

        var url = await rig.RunAsync(topic.PublicId, attachment.PublicId, Member);

        url.Should().Be("https://storage.example/presigned");
        rig.DownloadName.Should().Be("deck.pdf", "AC-164: the file downloads under its ORIGINAL name, not the storage key");
        await rig.Files.DidNotReceiveWithAnyArgs().GetPreSignedUrlAsync(default!, default!, default, default);
        rig.Expiry.Should().NotBeNull("the reader must pass an explicit expiry, never the store's default");
        rig.Expiry!.Value.Should().BePositive().And.BeLessThanOrEqualTo(TimeSpan.FromHours(1), "NFR-027 / AC-135 cap presigned URLs at 1 h");
        await rig.Audit.Received(1).EmitEnrichedAsync("Topics.AttachmentAccessed", "TopicAttachment",
            attachment.PublicId.ToString(), AuditOutcome.Success, Arg.Any<CancellationToken>());
    }

    [Fact] // FR-163: a Restricted topic outside the caller's grants reads as no such attachment - no URL, no row.
    public async Task A_restricted_topic_outside_the_callers_grants_is_not_found_and_writes_nothing()
    {
        var rig = new Rig();
        var (topic, attachment) = await SeedAsync(rig, restricted: true);

        (await rig.RunAsync(topic.PublicId, attachment.PublicId, Member)).Should().BeNull();

        await rig.Files.DidNotReceiveWithAnyArgs().GetDownloadUrlAsync(default!, default!, default!, default, default);
        rig.Audit.ReceivedCalls().Should().BeEmpty();
    }

    [Fact] // The same Restricted topic IS openable by a caller whose grant covers it.
    public async Task A_grant_on_the_restricted_topic_opens_it()
    {
        var rig = new Rig();
        var (topic, attachment) = await SeedAsync(rig, restricted: true);

        (await rig.RunAsync(topic.PublicId, attachment.PublicId, new TopicVisibilityScope(false, new[] { topic.PublicId })))
            .Should().NotBeNull();
    }

    [Fact] // The scope is the lookup: another topic's attachment id is not reachable through this topic.
    public async Task An_attachment_that_is_not_on_this_topic_is_not_found_and_writes_nothing()
    {
        var rig = new Rig();
        var (mine, _) = await SeedAsync(rig, key: "TOP-2026-041");
        var (_, theirs) = await SeedAsync(rig, key: "TOP-2026-042");

        (await rig.RunAsync(mine.PublicId, theirs.PublicId, Member)).Should().BeNull();

        rig.Audit.ReceivedCalls().Should().BeEmpty();
    }

    [Fact] // DEC-190 a1: no role list - the topic-detail read set; the reader's scope does the narrowing.
    public void The_query_names_no_roles()
    {
        new GetTopicAttachmentUrlQuery(Guid.NewGuid(), Guid.NewGuid()).AllowedRoles.Should().BeEmpty();
    }
}
