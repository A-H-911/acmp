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

    // Pinned RELEASE tag, no floating :latest (reproducible). Passed explicitly rather than relying on the
    // builder default: the parameterless ctor is obsolete, and an explicit tag means a Testcontainers
    // upgrade can't silently move which MinIO these tests run against. From quay.io, because minio/minio
    // was withdrawn from Docker Hub on 2026-09-11 (DEF-162); quay.io serves the same RELEASE tag.
    private readonly MinioContainer _minio = new MinioBuilder("quay.io/minio/minio:RELEASE.2023-01-31T02-24-19Z")
        .WithUsername("minioadmin")
        .WithPassword("minioadmin")
        .Build();

    private MinioFileStore _store = null!;

    public async Task InitializeAsync()
    {
        await ContainerStartup.StartOrFailFastAsync(_minio, "MinIO");
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

    // AC-164 / AC-165: a DOWNLOAD link, FETCHED from the real object store. The disposition is signed into the
    // query; this proves the signature survives the SDK's encoding of a non-ASCII value and that the store
    // answers with the attachment disposition and the original Arabic name - the part a unit test cannot see.
    [Fact]
    public async Task A_download_link_is_answered_with_an_attachment_disposition_carrying_the_arabic_name()
    {
        await _store.UploadAsync(Bucket, "topics/abc.pdf", Bytes("%PDF-1.7 real bytes"), "application/pdf");

        var url = await _store.GetDownloadUrlAsync(Bucket, "topics/abc.pdf", "تقرير المراجعة.pdf", TimeSpan.FromMinutes(10));
        using var http = new HttpClient();
        using var response = await http.GetAsync(url);

        response.IsSuccessStatusCode.Should().BeTrue($"the signed URL must be accepted, got {(int)response.StatusCode}");
        var disposition = response.Content.Headers.ContentDisposition!;
        disposition.DispositionType.Should().Be("attachment");
        disposition.FileNameStar.Should().Be("تقرير المراجعة.pdf");
        (await response.Content.ReadAsStringAsync()).Should().Be("%PDF-1.7 real bytes");

        // The inline link for the same object carries no disposition - the guest's Open is unchanged.
        using var inline = await http.GetAsync(await _store.GetPreSignedUrlAsync(Bucket, "topics/abc.pdf", TimeSpan.FromMinutes(10)));
        inline.Content.Headers.ContentDisposition.Should().BeNull();
    }
}
