using Acmp.Shared.Application.Abstractions;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Acmp.Shared.Infrastructure.FileStorage;

// IFileStore over self-hosted MinIO (ADR-0014). Creates the bucket on first write.
public sealed class MinioFileStore : IFileStore
{
    private readonly IMinioClient _client;
    private readonly MinioPresigner _presigner;

    public MinioFileStore(IMinioClient client, MinioPresigner presigner)
    {
        _client = client;
        _presigner = presigner;
    }

    public async Task<string> UploadAsync(string bucket, string objectName, Stream content, string contentType, CancellationToken ct = default)
    {
        await EnsureBucketAsync(bucket, ct);
        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectName)
            .WithStreamData(content)
            .WithObjectSize(content.Length)
            .WithContentType(contentType), ct);
        return objectName;
    }

    // Presigns with the public-endpoint client (browser-reachable via nginx) so the URL resolves + its SigV4
    // signature validates from the browser; upload/exists/delete keep using the fast internal client.
    public Task<string> GetPreSignedUrlAsync(string bucket, string objectName, TimeSpan expiry, CancellationToken ct = default) =>
        _presigner.Client.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectName)
            .WithExpiry((int)expiry.TotalSeconds));

    public async Task<bool> ExistsAsync(string bucket, string objectName, CancellationToken ct = default)
    {
        try
        {
            await _client.StatObjectAsync(new StatObjectArgs().WithBucket(bucket).WithObject(objectName), ct);
            return true;
        }
        catch (ObjectNotFoundException) { return false; }
        catch (BucketNotFoundException) { return false; }
    }

    public Task DeleteAsync(string bucket, string objectName, CancellationToken ct = default) =>
        _client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucket).WithObject(objectName), ct);

    // Auto-create is a LOCAL-DEV convenience, not a production path. Against S3 the bucket is provisioned
    // out of band by deploy/aws/02-s3.sh and the app's IAM policy grants no s3:CreateBucket (ADR-0035) —
    // and Block Public Access means a wrong name must fail loudly, not be silently created. The granted
    // s3:ListBucket makes HeadBucket succeed, so in cloud this probe returns true and MakeBucket never
    // fires; the catch is for a tightened policy where even the probe is denied. Swallowing a denial here
    // is safe because it decides nothing: PutObject immediately after is the real authority and its own
    // failure surfaces to the caller.
    private async Task EnsureBucketAsync(string bucket, CancellationToken ct)
    {
        try
        {
            if (await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct))
                return;
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct);
        }
        catch (Exception ex) when (ex is AccessDeniedException or AuthorizationException)
        {
            // Pre-provisioned bucket we may write but not administer — proceed to the write.
        }
    }
}

// Holds the IMinioClient used for presigning — the public-endpoint client when configured (browser-reachable
// via nginx), else the internal client. A distinct singleton so upload/exists/delete stay on the fast internal
// endpoint. Lives in this file so it inherits the MinioFileStore coverage exclusion (ADR-0016 §1).
public sealed class MinioPresigner
{
    public MinioPresigner(IMinioClient client) => Client = client;

    public IMinioClient Client { get; }
}
