using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Application.Internal;
using Acmp.Modules.Meetings.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Features.DeleteRecording;

// FR-056: remove a meeting's recording — an uploaded file OR a Webex reference. Secretary/Chairman only. For an
// uploaded recording the stored MinIO object is deleted (best-effort); a Webex reference is only cleared from
// the meeting record (we don't own / don't delete the Webex-hosted asset — a re-delivered recordings webhook
// could re-attach it). Idempotent. The removal is hash-chain audited even though the object is gone.
public sealed record DeleteRecordingCommand(string MeetingKey) : IRequest, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = new[] { AcmpRoles.Secretary, AcmpRoles.Chairman };
}

public sealed class DeleteRecordingHandler : IRequestHandler<DeleteRecordingCommand>
{
    public const string Bucket = "acmp-recordings";

    private readonly IMeetingsDbContext _db;
    private readonly IFileStore _files;
    private readonly ICurrentUser _user;
    private readonly IAuditSink _audit;

    public DeleteRecordingHandler(IMeetingsDbContext db, IFileStore files, ICurrentUser user, IAuditSink audit)
    {
        _db = db;
        _files = files;
        _user = user;
        _audit = audit;
    }

    public async Task Handle(DeleteRecordingCommand request, CancellationToken ct)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Key == request.MeetingKey, ct)
            ?? throw new KeyNotFoundException("Meeting not found.");

        var (sub, _) = CurrentActor.Of(_user);
        var objectKey = meeting.RecordingObjectKey;

        meeting.RemoveRecording();
        await _db.SaveChangesAsync(ct);

        // Delete the stored object only for an uploaded recording; best-effort (the reference is already cleared).
        if (objectKey is not null)
        {
            try { await _files.DeleteAsync(Bucket, objectKey, ct); }
            catch { /* ponytail: orphaned blob tolerated; a storage sweep can reclaim it if it ever matters */ }
        }

        await _audit.EmitEnrichedAsync("Meetings.RecordingRemoved", nameof(Meeting), meeting.PublicId.ToString(), ct: ct);
    }
}
