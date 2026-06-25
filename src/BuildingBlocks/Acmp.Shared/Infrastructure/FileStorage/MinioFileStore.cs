using Acmp.Shared.Application.Abstractions;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Acmp.Shared.Infrastructure.FileStorage;

// IFileStore over self-hosted MinIO (ADR-0014). Creates the bucket on first write.
public sealed class MinioFileStore : IFileStore
{
    private readonly IMinioClient _client;

    public MinioFileStore(IMinioClient client) => _client = client;

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

    public Task<string> GetPreSignedUrlAsync(string bucket, string objectName, TimeSpan expiry, CancellationToken ct = default) =>
        _client.PresignedGetObjectAsync(new PresignedGetObjectArgs()
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
