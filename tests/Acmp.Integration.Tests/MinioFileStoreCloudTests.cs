using System.Text;
using Acmp.Shared.Infrastructure.FileStorage;
using FluentAssertions;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using NSubstitute;

namespace Acmp.Integration.Tests;

// The S3-facing half of MinioFileStore (slice P22 / ADR-0035), covered WITHOUT AWS and without Docker.
// MinioFileStoreTests already proves the store against a real MinIO container; what could not be proven
// there is how the same adapter behaves against Amazon S3, where the bucket is pre-provisioned by
// deploy/aws/02-s3.sh and the app's IAM policy is narrower than a MinIO root user's.
public sealed class MinioFileStoreCloudTests
{
    private static MemoryStream Bytes(string s) => new(Encoding.UTF8.GetBytes(s));

    // Least-privilege tolerance: the cloud IAM policy grants no s3:CreateBucket, and a tightened policy
    // could deny the HeadBucket probe too. Neither may abort a write to a bucket that already exists.
    [Fact]
    public async Task Upload_still_writes_when_the_bucket_probe_and_create_are_denied()
    {
        var client = Substitute.For<IMinioClient>();
        client.BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new AccessDeniedException("s3:ListBucket denied"));

        var store = new MinioFileStore(client, new MinioPresigner(client));

        var key = await store.UploadAsync("acmp-prod-recordings", "MTG-2026-001/a.mp4", Bytes("x"), "video/mp4");

        key.Should().Be("MTG-2026-001/a.mp4");
        await client.DidNotReceive().MakeBucketAsync(Arg.Any<MakeBucketArgs>(), Arg.Any<CancellationToken>());
        await client.Received(1).PutObjectAsync(Arg.Any<PutObjectArgs>(), Arg.Any<CancellationToken>());
    }

    // A denial is tolerated; a REAL storage failure must still reach the caller (fail-closed, no silent
    // "upload succeeded" on an unreachable store).
    [Fact]
    public async Task Upload_still_fails_when_the_store_itself_is_broken()
    {
        var client = Substitute.For<IMinioClient>();
        client.BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new UnexpectedMinioException("connection reset"));

        var store = new MinioFileStore(client, new MinioPresigner(client));

        await FluentActions
            .Awaiting(() => store.UploadAsync("b", "k", Bytes("x"), "video/mp4"))
            .Should().ThrowAsync<UnexpectedMinioException>();
    }

    // What the presigned playback URL actually looks like against a real AWS endpoint — the U1 spike
    // question that does NOT need an AWS account, because presigning is a local SigV4 computation once the
    // region is configured. Two things are pinned here:
    //   1. the ORIGIN, which is what the nginx CSP media-src must allow. Minio 6.0.3 rewrites any Amazon
    //      host through AWSS3Endpoints, where us-east-1 maps to the legacy global "s3.amazonaws.com" — NOT
    //      the "s3.us-east-1.amazonaws.com" that deploy/.env.cloud.example configures — and addresses the
    //      bucket path-style. Allowing the configured host in CSP would block playback.
    //   2. the SigV4 credential scope, which must carry the configured region or S3 answers
    //      AuthorizationHeaderMalformed.
    [Fact]
    public async Task Presigned_url_for_an_aws_endpoint_uses_the_amazon_host_and_signs_in_region()
    {
        var aws = new MinioClient()
            .WithEndpoint("s3.us-east-1.amazonaws.com")
            .WithCredentials("AKIAEXAMPLE", "secretexample")
            .WithSSL(true)
            .WithRegion("us-east-1")
            .Build();
        var store = new MinioFileStore(aws, new MinioPresigner(aws));

        var url = await store.GetPreSignedUrlAsync("acmp-prod-recordings", "MTG-2026-001/a.mp4", TimeSpan.FromMinutes(10));

        new Uri(url).GetLeftPart(UriPartial.Authority).Should().Be("https://s3.amazonaws.com");
        url.Should().Contain("acmp-prod-recordings/MTG-2026-001/a.mp4");
        url.Should().Contain("%2Fus-east-1%2Fs3%2Faws4_request");
    }
}
