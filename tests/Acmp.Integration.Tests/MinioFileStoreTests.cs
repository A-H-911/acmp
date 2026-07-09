using System.Text;
using Acmp.Shared.Infrastructure.FileStorage;
using FluentAssertions;
using Minio;
using Testcontainers.Minio;

namespace Acmp.Integration.Tests;

// Real-adapter coverage for MinioFileStore (ADR-0016 / FR-056). Boots ONE MinIO container (Testcontainers,
// pinned image via the package default) and exercises every branch of the production store — bucket
// auto-create on first write + skip on the second, presign, ExistsAsync (found / object-missing /
// bucket-missing), and delete. This replaces the former blanket coverage exclusion: the store now stands
// on the same real-infrastructure footing as the SQL backstop suite. Requires a running Docker daemon.
public sealed class MinioFileStoreTests : IAsyncLifetime
{
    private const string Bucket = "acmp-recordings";

    // Default builder image is a pinned RELEASE tag (reproducible; no floating :latest pull).
    private readonly MinioContainer _minio = new MinioBuilder()
        .WithUsername("minioadmin")
        .WithPassword("minioadmin")
        .Build();

    private MinioFileStore _store = null!;

    public async Task InitializeAsync()
    {
        await _minio.StartAsync();
        var uri = new Uri(_minio.GetConnectionString());
        var client = new MinioClient()
            .WithEndpoint(uri.Host, uri.Port)
            .WithCredentials("minioadmin", "minioadmin")
            .Build();
        _store = new MinioFileStore(client, new MinioPresigner(client));
    }

    public Task DisposeAsync() => _minio.DisposeAsync().AsTask();

    private static MemoryStream Bytes(string s) => new(Encoding.UTF8.GetBytes(s));

    [Fact]
    public async Task Upload_presign_exists_delete_covers_all_adapter_branches()
    {
        // First write: EnsureBucket sees no bucket → creates it. Returns the server-derived object key.
        var key = await _store.UploadAsync(Bucket, "mtg/one.mp4", Bytes("first"), "video/mp4");
        key.Should().Be("mtg/one.mp4");

        // Second write: EnsureBucket sees the bucket already → skips MakeBucket (the other branch).
        await _store.UploadAsync(Bucket, "mtg/two.mp4", Bytes("second"), "video/mp4");

        // ExistsAsync — the three paths the production ExistsAsync must handle.
        (await _store.ExistsAsync(Bucket, "mtg/one.mp4")).Should().BeTrue();        // StatObject succeeds
        (await _store.ExistsAsync(Bucket, "mtg/missing.mp4")).Should().BeFalse();   // ObjectNotFound → false
        (await _store.ExistsAsync("no-such-bucket", "x")).Should().BeFalse();       // BucketNotFound → false

        // Presign returns a signed URL that carries the object path (browser-playback capability).
        var url = await _store.GetPreSignedUrlAsync(Bucket, "mtg/one.mp4", TimeSpan.FromMinutes(10));
        url.Should().Contain("mtg/one.mp4");

        // Delete removes the object; a subsequent stat now misses.
        await _store.DeleteAsync(Bucket, "mtg/one.mp4");
        (await _store.ExistsAsync(Bucket, "mtg/one.mp4")).Should().BeFalse();
    }
}
