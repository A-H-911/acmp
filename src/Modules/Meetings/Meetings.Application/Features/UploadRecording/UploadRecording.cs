using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Contracts;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Modules.Meetings.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Acmp.Modules.Meetings.Application.Features.UploadRecording;

// FR-056: upload a meeting recording file (the local/cloud MP4 a secretary holds). Size/MIME are validated
// against MeetingRecordingOptions before the stream is read (400, no partial store). Bytes go to MinIO via the
// shared IFileStore (ADR-0014); the object key + display metadata are stored on the Meeting. Playback is served
// later via a short-lived pre-signed URL (GetPreSignedUrlAsync), never streamed through the API. RBAC =
// Minutes.Capture (Secretary/Chairman) — a recording is captured meeting content, same role set as the minutes.
public sealed record UploadRecordingCommand(
    string MeetingKey, string FileName, string ContentType, long SizeBytes, Stream Content)
    : IRequest<RecordingDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Secretary, AcmpRoles.Chairman };
}

public sealed class UploadRecordingValidator : AbstractValidator<UploadRecordingCommand>
{
    public UploadRecordingValidator(IOptions<MeetingRecordingOptions> options)
    {
        var o = options.Value;
        RuleFor(x => x.MeetingKey).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.SizeBytes)
            .GreaterThan(0)
            .LessThanOrEqualTo(o.MaxSizeBytes)
            .WithMessage($"Recording exceeds the maximum allowed size of {o.MaxSizeBytes / (1024 * 1024)} MB.");
        RuleFor(x => x.ContentType)
            .Must(ct => o.AllowedContentTypes.Contains(ct, StringComparer.OrdinalIgnoreCase))
            .WithMessage(x => $"Recording type '{x.ContentType}' is not allowed.");
    }
}

public sealed class UploadRecordingHandler : IRequestHandler<UploadRecordingCommand, RecordingDto>
{
    public const string Bucket = "acmp-recordings";

    private readonly IMeetingsDbContext _db;
    private readonly IFileStore _files;
    private readonly ICurrentUser _user;
    private readonly IAuditSink _audit;
    private readonly IFileContentInspector _inspector;

    public UploadRecordingHandler(IMeetingsDbContext db, IFileStore files, ICurrentUser user, IAuditSink audit,
        IFileContentInspector inspector)
    {
        _db = db;
        _files = files;
        _user = user;
        _audit = audit;
        _inspector = inspector;
    }

    public async Task<RecordingDto> Handle(UploadRecordingCommand request, CancellationToken ct)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Key == request.MeetingKey, ct)
            ?? throw new KeyNotFoundException("Meeting not found.");

        var (sub, _) = CurrentActor.Of(_user);
        var previousKey = meeting.RecordingObjectKey;

        // C-FILE-01: the declared ContentType was allow-list-checked in the validator; now confirm the actual
        // bytes match it (magic-byte sniff) so a mislabelled payload can't slip through. Fail-closed, pre-store.
        if (!_inspector.ContentMatchesDeclared(request.Content, request.ContentType))
            throw new ValidationException($"Recording content does not match its declared type '{request.ContentType}'.");

        // Object key is server-derived: meeting key + GUID + a content-type extension — NEVER the raw client
        // filename. A name with spaces/parens/unicode would break the SigV4 presigned-URL signature (encoding
        // mismatch across SDK/proxy/MinIO), and a crafted name could traverse the namespace. The original
        // filename is kept only as display metadata (RecordingFileName).
        var objectName = $"{meeting.Key}/{Guid.NewGuid()}{ExtensionFor(request.ContentType)}";
        var storageKey = await _files.UploadAsync(Bucket, objectName, request.Content, request.ContentType, ct);

        meeting.AttachUploadedRecording(storageKey, request.FileName, request.ContentType, request.SizeBytes);
        await _db.SaveChangesAsync(ct);

        // Best-effort cleanup of a superseded object — an orphaned blob is harmless; never fail the upload on it.
        if (previousKey is not null && previousKey != storageKey)
        {
            try { await _files.DeleteAsync(Bucket, previousKey, ct); }
            catch { /* ponytail: orphan tolerated; a storage sweep can reclaim it if it ever matters */ }
        }

        await _audit.EmitEnrichedAsync("Meetings.RecordingUploaded", nameof(Meeting), meeting.PublicId.ToString(), ct: ct);

        return new RecordingDto("Uploaded", request.FileName, request.ContentType,
            request.SizeBytes, meeting.RecordingDurationSeconds, null);
    }

    // Extension for the stored object, from the already-validated content type (the allowlist in the
    // validator guarantees one of these; the fallback is defensive only).
    private static string ExtensionFor(string contentType) => contentType.ToLowerInvariant() switch
    {
        "video/mp4" => ".mp4",
        "video/webm" => ".webm",
        "video/quicktime" => ".mov",
        _ => ".bin",
    };
}
