using System.Text;
using Acmp.Application.Tests.Shared;
using Acmp.Modules.Meetings.Application;
using Acmp.Modules.Meetings.Application.Features.DeleteRecording;
using Acmp.Modules.Meetings.Application.Features.GetMeetingDetail;
using Acmp.Modules.Meetings.Application.Features.GetRecordingUrl;
using Acmp.Modules.Meetings.Application.Features.UploadRecording;
using Acmp.Modules.Meetings.Domain;
using Acmp.Modules.Meetings.Domain.Enums;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Membership;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Acmp.Application.Tests.Meetings;

// FR-056: manual recording upload — size/MIME validation, store-to-MinIO via IFileStore, metadata + audit.
public class MeetingRecordingTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 15, 9, 0, 0, TimeSpan.Zero);
    private static readonly IOptions<MeetingRecordingOptions> Options =
        Microsoft.Extensions.Options.Options.Create(new MeetingRecordingOptions());

    // These tests exercise the upload/store/audit logic, not content sniffing (C-FILE-01), so the inspector
    // is a pass-through; the magic-byte behaviour is covered by MimeFileContentInspectorTests.
    private static readonly IFileContentInspector PassInspector = new PassThroughInspector();

    // DEF-015: bucket names now come from configuration. Defaults reproduce the historical constants, so
    // every existing assertion below still names the same bucket; the per-environment override is asserted
    // by its own test.
    private static readonly IOptions<StorageOptions> Buckets = Microsoft.Extensions.Options.Options.Create(new StorageOptions());

    private sealed class PassThroughInspector : IFileContentInspector
    {
        public bool ContentMatchesDeclared(Stream content, string declaredContentType) => true;
    }

    [Fact]
    public void Validator_rejects_oversize_disallowed_type_and_empty_name()
    {
        var v = new UploadRecordingValidator(Options);
        var ok = new UploadRecordingCommand("MTG-2026-001", "rec.mp4", "video/mp4", 1024, Stream.Null);
        v.Validate(ok).IsValid.Should().BeTrue();

        v.Validate(ok with { SizeBytes = 3L * 1024 * 1024 * 1024 }).IsValid.Should().BeFalse(); // > 2 GB default
        v.Validate(ok with { ContentType = "application/x-msdownload" }).IsValid.Should().BeFalse();
        v.Validate(ok with { FileName = "" }).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Valid_upload_stores_metadata_and_emits_audit()
    {
        var (db, user) = NewDb();
        var meeting = SeedMeeting(db);

        var files = Substitute.For<IFileStore>();
        files.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult($"{ci.ArgAt<string>(0)}/{ci.ArgAt<string>(1)}"));
        var audit = Substitute.For<IAuditSink>();

        var content = new MemoryStream(Encoding.UTF8.GetBytes("mp4-bytes"));
        var dto = await new UploadRecordingHandler(db, files, user, audit, PassInspector, Buckets)
            .Handle(new UploadRecordingCommand(meeting.Key, "board.mp4", "video/mp4", 9, content), CancellationToken.None);

        dto.Source.Should().Be("Uploaded");
        dto.FileName.Should().Be("board.mp4");
        var stored = await db.Meetings.SingleAsync(m => m.Key == meeting.Key);
        stored.RecordingObjectKey.Should().StartWith("acmp-recordings/");
        stored.RecordingFileName.Should().Be("board.mp4");
        stored.RecordingContentType.Should().Be("video/mp4");
        stored.RecordingSizeBytes.Should().Be(9);
        await files.Received(1).UploadAsync("acmp-recordings", Arg.Any<string>(), Arg.Any<Stream>(), "video/mp4", Arg.Any<CancellationToken>());
        await audit.Received(1).EmitEnrichedAsync("Meetings.RecordingUploaded", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Replacing_a_recording_deletes_the_previous_object()
    {
        var (db, user) = NewDb();
        var meeting = SeedMeeting(db);
        meeting.AttachUploadedRecording("acmp-recordings/old-key", "old.mp4", "video/mp4", 5);
        await db.SaveChangesAsync();

        var files = Substitute.For<IFileStore>();
        files.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("acmp-recordings/new-key");
        var audit = Substitute.For<IAuditSink>();

        await new UploadRecordingHandler(db, files, user, audit, PassInspector, Buckets)
            .Handle(new UploadRecordingCommand(meeting.Key, "new.mp4", "video/mp4", 9, Stream.Null), CancellationToken.None);

        await files.Received(1).DeleteAsync("acmp-recordings", "acmp-recordings/old-key", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unknown_meeting_throws()
    {
        var (db, user) = NewDb();
        var handler = new UploadRecordingHandler(db, Substitute.For<IFileStore>(), user, Substitute.For<IAuditSink>(), PassInspector, Buckets);
        await FluentActions.Awaiting(() => handler.Handle(
                new UploadRecordingCommand("MTG-9999-999", "x.mp4", "video/mp4", 1, Stream.Null), CancellationToken.None))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Best_effort_delete_failure_does_not_fail_the_upload()
    {
        var (db, user) = NewDb();
        var meeting = SeedMeeting(db);
        meeting.AttachUploadedRecording("acmp-recordings/old-key", "old.mp4", "video/mp4", 5);
        await db.SaveChangesAsync();

        var files = Substitute.For<IFileStore>();
        files.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("acmp-recordings/new-key");
        files.DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("storage down")));

        var dto = await new UploadRecordingHandler(db, files, user, Substitute.For<IAuditSink>(), PassInspector, Buckets)
            .Handle(new UploadRecordingCommand(meeting.Key, "new.mp4", "video/mp4", 9, Stream.Null), CancellationToken.None);

        dto.FileName.Should().Be("new.mp4"); // upload succeeded despite the cleanup failure
        (await db.Meetings.SingleAsync(m => m.Key == meeting.Key)).RecordingObjectKey.Should().Be("acmp-recordings/new-key");
    }

    [Fact]
    public async Task Detail_projects_recording_by_source()
    {
        var (db, user) = NewDb();
        SeedMeeting(db, "MTG-2026-010").AttachUploadedRecording("acmp-recordings/k", "a.mp4", "video/mp4", 12);
        SeedMeeting(db, "MTG-2026-011").AttachRecording("https://webex/play", "https://webex/dl", 600);
        SeedMeeting(db, "MTG-2026-012"); // no recording
        await db.SaveChangesAsync();

        // DEF-073: the caller is a committee member (the substitute reports no roles), so the guest
        // scoping is inactive here and the directory is never consulted.
        var handler = new GetMeetingDetailHandler(db, Substitute.For<ICommitteeDirectory>(), user, TopicConfidentialityStub.SeesEverything());
        (await handler.Handle(new GetMeetingDetailQuery("MTG-2026-010"), default))!.Recording!.Source.Should().Be("Uploaded");
        var w = (await handler.Handle(new GetMeetingDetailQuery("MTG-2026-011"), default))!.Recording!;
        w.Source.Should().Be("Webex");
        w.PlaybackUrl.Should().Be("https://webex/play");
        w.DurationSeconds.Should().Be(600);
        (await handler.Handle(new GetMeetingDetailQuery("MTG-2026-012"), default))!.Recording.Should().BeNull();
    }

    [Fact]
    public async Task Recording_url_is_presigned_for_an_uploaded_recording()
    {
        var (db, _) = NewDb();
        var meeting = SeedMeeting(db);
        meeting.AttachUploadedRecording("acmp-recordings/MTG-2026-001/abc.mp4", "a.mp4", "video/mp4", 10);
        await db.SaveChangesAsync();

        var files = Substitute.For<IFileStore>();
        files.GetPreSignedUrlAsync("acmp-recordings", "acmp-recordings/MTG-2026-001/abc.mp4", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://minio.test/signed");

        var url = await new GetRecordingUrlHandler(db, files, Buckets, Substitute.For<IAuditSink>()).Handle(new GetRecordingUrlQuery(meeting.Key), CancellationToken.None);
        url.Should().Be("https://minio.test/signed");
    }

    // NFR-027 - "time-limited, <= 1 h expiry". Until this test the ceiling was a VALUE, not a property:
    // every other test here passes Arg.Any<TimeSpan>(), so raising Ttl to FromHours(4) broke nothing.
    // A presigned URL is a bearer-less capability for its whole TTL, so the ceiling is the control.
    [Fact]
    public async Task Playback_url_expiry_stays_inside_the_one_hour_ceiling()
    {
        var (db, _) = NewDb();
        var meeting = SeedMeeting(db);
        meeting.AttachUploadedRecording("acmp-recordings/MTG-2026-001/abc.mp4", "a.mp4", "video/mp4", 10);
        await db.SaveChangesAsync();

        TimeSpan? captured = null;
        var files = Substitute.For<IFileStore>();
        files.GetPreSignedUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ci => { captured = ci.ArgAt<TimeSpan>(2); return "https://minio.test/signed"; });

        await new GetRecordingUrlHandler(db, files, Buckets, Substitute.For<IAuditSink>())
            .Handle(new GetRecordingUrlQuery(meeting.Key), CancellationToken.None);

        captured.Should().NotBeNull("the handler must pass an explicit expiry, never the store's default");
        captured!.Value.Should().BePositive("a zero or negative expiry would be signed as already dead");
        captured.Value.Should().BeLessThanOrEqualTo(TimeSpan.FromHours(1), "NFR-027 caps presigned URLs at 1 h");
    }

    // NFR-025 / AC-122 / DEF-094 - "all access to transcript content shall generate an audit log entry",
    // which was 0% before this: the handler emitted nothing at all. Minting a presigned URL IS the access
    // event, because the URL is a bearer-less capability good for its whole TTL.
    [Fact]
    public async Task Minting_a_playback_url_writes_an_access_audit_row()
    {
        var (db, _) = NewDb();
        var meeting = SeedMeeting(db);
        meeting.AttachUploadedRecording("acmp-recordings/MTG-2026-001/abc.mp4", "a.mp4", "video/mp4", 10);
        await db.SaveChangesAsync();

        var files = Substitute.For<IFileStore>();
        files.GetPreSignedUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("https://minio.test/signed");
        var audit = Substitute.For<IAuditSink>();

        await new GetRecordingUrlHandler(db, files, Buckets, audit)
            .Handle(new GetRecordingUrlQuery(meeting.Key), CancellationToken.None);

        await audit.Received(1).EmitEnrichedAsync("Meetings.RecordingAccessed", "Meeting",
            meeting.PublicId.ToString(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact] // The row must mean "a capability was handed out", not "someone asked" - a 404 grants nothing.
    public async Task A_meeting_with_no_recording_writes_no_access_audit_row()
    {
        var (db, _) = NewDb();
        var meeting = SeedMeeting(db);          // seeded WITHOUT AttachUploadedRecording
        await db.SaveChangesAsync();

        var audit = Substitute.For<IAuditSink>();
        var url = await new GetRecordingUrlHandler(db, Substitute.For<IFileStore>(), Buckets, audit)
            .Handle(new GetRecordingUrlQuery(meeting.Key), CancellationToken.None);

        url.Should().BeNull();
        await audit.DidNotReceive().EmitEnrichedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // DEF-015: the bucket was a const on each handler, so UAT and production would have written to the same
    // bucket (AC-083). Every recording handler must now name the bucket its ENVIRONMENT configures.
    [Fact]
    public async Task Recording_handlers_use_the_configured_bucket_not_a_constant()
    {
        var envBucket = Microsoft.Extensions.Options.Options.Create(
            new StorageOptions { RecordingsBucket = "acmp-prod-recordings" });
        var (db, user) = NewDb();
        var meeting = SeedMeeting(db);
        var files = Substitute.For<IFileStore>();
        files.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<string>(1));

        await new UploadRecordingHandler(db, files, user, Substitute.For<IAuditSink>(), PassInspector, envBucket)
            .Handle(new UploadRecordingCommand(meeting.Key, "a.mp4", "video/mp4", 4, new MemoryStream(Encoding.UTF8.GetBytes("mp4-bytes"))), CancellationToken.None);
        await new GetRecordingUrlHandler(db, files, envBucket, Substitute.For<IAuditSink>())
            .Handle(new GetRecordingUrlQuery(meeting.Key), CancellationToken.None);
        await new DeleteRecordingHandler(db, files, user, Substitute.For<IAuditSink>(), envBucket)
            .Handle(new DeleteRecordingCommand(meeting.Key), CancellationToken.None);

        await files.Received(1).UploadAsync("acmp-prod-recordings", Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await files.Received(1).GetPreSignedUrlAsync("acmp-prod-recordings", Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await files.Received(1).DeleteAsync("acmp-prod-recordings", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Recording_url_is_null_when_no_uploaded_recording()
    {
        var (db, _) = NewDb();
        var meeting = SeedMeeting(db);
        var files = Substitute.For<IFileStore>();

        var url = await new GetRecordingUrlHandler(db, files, Buckets, Substitute.For<IAuditSink>()).Handle(new GetRecordingUrlQuery(meeting.Key), CancellationToken.None);

        url.Should().BeNull();
        await files.DidNotReceive().GetPreSignedUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_removes_an_uploaded_recording_object_and_reference()
    {
        var (db, user) = NewDb();
        var meeting = SeedMeeting(db);
        meeting.AttachUploadedRecording("acmp-recordings/k.mp4", "a.mp4", "video/mp4", 9);
        await db.SaveChangesAsync();
        var files = Substitute.For<IFileStore>();
        var audit = Substitute.For<IAuditSink>();

        await new DeleteRecordingHandler(db, files, user, audit, Buckets)
            .Handle(new DeleteRecordingCommand(meeting.Key), CancellationToken.None);

        (await db.Meetings.SingleAsync(m => m.Key == meeting.Key)).RecordingObjectKey.Should().BeNull();
        await files.Received(1).DeleteAsync("acmp-recordings", "acmp-recordings/k.mp4", Arg.Any<CancellationToken>());
        await audit.Received(1).EmitEnrichedAsync("Meetings.RecordingRemoved", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_clears_a_webex_reference_without_touching_storage()
    {
        var (db, user) = NewDb();
        var meeting = SeedMeeting(db);
        meeting.AttachRecording("https://webex/play", "https://webex/dl", 600);
        await db.SaveChangesAsync();
        var files = Substitute.For<IFileStore>();

        await new DeleteRecordingHandler(db, files, user, Substitute.For<IAuditSink>(), Buckets)
            .Handle(new DeleteRecordingCommand(meeting.Key), CancellationToken.None);

        var stored = await db.Meetings.SingleAsync(m => m.Key == meeting.Key);
        stored.RecordingUrl.Should().BeNull();
        stored.RecordingDurationSeconds.Should().BeNull();
        await files.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_ignores_a_storage_failure()
    {
        var (db, user) = NewDb();
        var meeting = SeedMeeting(db);
        meeting.AttachUploadedRecording("acmp-recordings/k.mp4", "a.mp4", "video/mp4", 9);
        await db.SaveChangesAsync();
        var files = Substitute.For<IFileStore>();
        files.DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("storage down")));

        await new DeleteRecordingHandler(db, files, user, Substitute.For<IAuditSink>(), Buckets)
            .Handle(new DeleteRecordingCommand(meeting.Key), CancellationToken.None);

        (await db.Meetings.SingleAsync(m => m.Key == meeting.Key)).RecordingObjectKey.Should().BeNull();
    }

    [Fact]
    public async Task Delete_unknown_meeting_throws()
    {
        var (db, user) = NewDb();
        var handler = new DeleteRecordingHandler(db, Substitute.For<IFileStore>(), user, Substitute.For<IAuditSink>(), Buckets);
        await FluentActions.Awaiting(() => handler.Handle(new DeleteRecordingCommand("MTG-9999-999"), CancellationToken.None))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Theory] // object key carries a content-type extension, never the client filename (SigV4-safe key)
    [InlineData("video/mp4", ".mp4")]
    [InlineData("video/webm", ".webm")]
    [InlineData("video/quicktime", ".mov")]
    [InlineData("video/x-other", ".bin")]
    public async Task Object_key_extension_derives_from_content_type(string contentType, string ext)
    {
        var (db, user) = NewDb();
        var meeting = SeedMeeting(db);
        var files = Substitute.For<IFileStore>();
        files.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult($"{ci.ArgAt<string>(0)}/{ci.ArgAt<string>(1)}"));

        await new UploadRecordingHandler(db, files, user, Substitute.For<IAuditSink>(), PassInspector, Buckets)
            .Handle(new UploadRecordingCommand(meeting.Key, "clip.any", contentType, 3, Stream.Null), CancellationToken.None);

        (await db.Meetings.SingleAsync(m => m.Key == meeting.Key)).RecordingObjectKey.Should().EndWith(ext);
    }

    private static (MeetingsDbContext, ICurrentUser) NewDb()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns("kc-sara");
        user.DisplayName.Returns("Sara K.");
        var db = new MeetingsDbContext(
            new DbContextOptionsBuilder<MeetingsDbContext>().UseInMemoryDatabase("rec-" + Guid.NewGuid()).Options,
            clock, user);
        return (db, user);
    }

    private static Meeting SeedMeeting(MeetingsDbContext db, string key = "MTG-2026-001")
    {
        var meeting = Meeting.Schedule(key, "Board", Meeting.SingleCommitteeId,
            Guid.NewGuid(), "Chair", Now, Now.AddHours(1), MeetingType.Regular, MeetingMode.Remote, null, null, Now);
        db.Meetings.Add(meeting);
        db.SaveChanges();
        return meeting;
    }
}
