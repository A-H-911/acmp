using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Acmp.Modules.Meetings.Application.Features.GetRecordingUrl;

// Mint a short-lived presigned URL so the browser can stream an uploaded recording directly from MinIO
// (ADR-0014). Returns null when the meeting has no uploaded recording (→ 404). Webex recordings surface
// their playback URL on the meeting detail instead and are not served here.
//
// ⚠ NFR-025 / AC-122 / DEF-094 — THIS READ WAS OPEN TO ANY COMMITTEE MEMBER AND WAS NOT AUDITED. NFR-025
// (priority Must) says recording and transcript data is accessible only to Chairman, Secretary and Auditor,
// and that ALL access generates an audit entry. Neither held: AllowedRoles was empty and the handler emitted
// nothing, so "100% of transcript reads logged" was 0%. Both are fixed here rather than the requirement being
// reworded to match the code (DEC-064 d-DEF-094).
//
// ⚠ THE URL IS THE ASSET, NOT A POINTER TO IT. A presigned URL is a bearer-less capability: whoever holds it
// can stream the recording for its whole TTL, with no further authentication. That is why minting one is an
// ACCESS EVENT worth auditing even though the request looks like a read, and why the role gate belongs here
// and not only on the upload.
public sealed record GetRecordingUrlQuery(string Key) : IRequest<string?>, IAuthorizedRequest
{
    // The three roles NFR-025 names, and no others. Deliberately NOT the upload set (Secretary/Chairman):
    // the Auditor may read what they must review but may not add or remove recordings.
    public IReadOnlyCollection<string> AllowedRoles { get; } =
        new[] { AcmpRoles.Chairman, AcmpRoles.Secretary, AcmpRoles.Auditor };
}

public sealed class GetRecordingUrlHandler : IRequestHandler<GetRecordingUrlQuery, string?>
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private readonly IMeetingsDbContext _db;
    private readonly IFileStore _files;
    private readonly string _bucket;
    private readonly IAuditSink _audit;

    public GetRecordingUrlHandler(IMeetingsDbContext db, IFileStore files, IOptions<StorageOptions> storage,
        IAuditSink audit)
    {
        _db = db;
        _files = files;
        _bucket = storage.Value.RecordingsBucket;   // DEF-015: per-environment, never a const
        _audit = audit;
    }

    public async Task<string?> Handle(GetRecordingUrlQuery request, CancellationToken ct)
    {
        var meeting = await _db.Meetings
            .Where(m => m.Key == request.Key)
            .Select(m => new { m.PublicId, m.RecordingObjectKey })
            .FirstOrDefaultAsync(ct);

        if (meeting is null || string.IsNullOrEmpty(meeting.RecordingObjectKey))
            return null;

        var url = await _files.GetPreSignedUrlAsync(_bucket, meeting.RecordingObjectKey, Ttl, ct);

        // NFR-025: audited AFTER the URL is successfully minted, so the row means "a capability was handed
        // out" rather than "someone asked". A 404 for a meeting with no recording grants nothing and is not
        // an access event; the authorization REFUSAL is already audited upstream by
        // AuditingAuthorizationResultHandler, so this handler covers only the success path it owns.
        //
        // ⚠ THE URL ITSELF IS NEVER RECORDED. NFR-027 requires presigned URLs to stay out of logs and
        // notifications, and an audit row is a log. The subject id is enough to answer "who obtained access
        // to this meeting's recording, and when".
        await _audit.EmitEnrichedAsync("Meetings.RecordingAccessed", nameof(Meeting),
            meeting.PublicId.ToString(), ct: ct);

        return url;
    }
}
