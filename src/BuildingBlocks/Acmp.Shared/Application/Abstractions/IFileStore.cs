namespace Acmp.Shared.Application.Abstractions;

// Object storage abstraction (ADR-0014). v1 implementation = self-hosted MinIO. Sensitive files
// are served via short-lived pre-signed URLs rather than streamed through the API.
public interface IFileStore
{
    Task<string> UploadAsync(string bucket, string objectName, Stream content, string contentType, CancellationToken ct = default);
    Task<string> GetPreSignedUrlAsync(string bucket, string objectName, TimeSpan expiry, CancellationToken ct = default);
    // AC-164 / AC-165: a link that DOWNLOADS the object under `downloadFileName` (a signed
    // Content-Disposition: attachment) instead of opening it inline. Objects are stored under server-derived
    // keys (C-FILE-01), so without this a saved file is named after its GUID.
    Task<string> GetDownloadUrlAsync(string bucket, string objectName, string downloadFileName, TimeSpan expiry, CancellationToken ct = default);
    Task<bool> ExistsAsync(string bucket, string objectName, CancellationToken ct = default);
    Task DeleteAsync(string bucket, string objectName, CancellationToken ct = default);
}
