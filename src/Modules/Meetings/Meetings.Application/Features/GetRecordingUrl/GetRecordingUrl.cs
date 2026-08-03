using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Shared.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Acmp.Modules.Meetings.Application.Features.GetRecordingUrl;

// Mint a short-lived presigned URL so the browser can stream an uploaded recording directly from MinIO
// (ADR-0014). Any committee member may view (read); the URL is a bearer-less capability with a short TTL.
// Returns null when the meeting has no uploaded recording (→ 404). Webex recordings surface their playback
// URL on the meeting detail instead and are not served here.
public sealed record GetRecordingUrlQuery(string Key) : IRequest<string?>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = Array.Empty<string>();
}

public sealed class GetRecordingUrlHandler : IRequestHandler<GetRecordingUrlQuery, string?>
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private readonly IMeetingsDbContext _db;
    private readonly IFileStore _files;
    private readonly string _bucket;

    public GetRecordingUrlHandler(IMeetingsDbContext db, IFileStore files, IOptions<StorageOptions> storage)
    {
        _db = db;
        _files = files;
        _bucket = storage.Value.RecordingsBucket;   // DEF-015: per-environment, never a const
    }

    public async Task<string?> Handle(GetRecordingUrlQuery request, CancellationToken ct)
    {
        var objectKey = await _db.Meetings
            .Where(m => m.Key == request.Key)
            .Select(m => m.RecordingObjectKey)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrEmpty(objectKey)
            ? null
            : await _files.GetPreSignedUrlAsync(_bucket, objectKey, Ttl, ct);
    }
}
