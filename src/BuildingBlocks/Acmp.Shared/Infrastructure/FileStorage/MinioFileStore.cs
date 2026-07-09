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

    private async Task EnsureBucketAsync(string bucket, CancellationToken ct)
    {
        var exists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct);
        if (!exists)
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct);
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
