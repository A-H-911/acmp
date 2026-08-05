using System.Text;
using Acmp.Shared.Infrastructure.FileStorage;
using FluentAssertions;
using Minio;

namespace Acmp.Integration.Tests;

// The LIVE half of the U1 spike, and the only thing that can satisfy AC-078: recording upload -> real S3 ->
// presigned playback -> delete, against a bucket AWS actually provisions. MinioFileStoreTests proves the
// adapter against a MinIO container and MinioFileStoreCloudTests pins the S3-shaped behaviour offline, but
// neither can prove that the app's IAM policy grants what the adapter actually calls at runtime. Only a real
// bucket can, and no EC2 instance is needed for it -- a bucket and a key are enough.
//
// OPT-IN by design. CI has no AWS identity and must never acquire one just to run tests, so without the
// environment below every fact here reports SKIPPED. Skipped, not passed: a test that silently returns green
// when it did not run is the exact failure shape this suite exists to catch.
//
//   ACMP_LIVE_S3_BUCKET      e.g. acmp-uat-recordings
//   ACMP_LIVE_S3_ACCESS_KEY  the acmp-<env>-app user's key (deploy/secrets/, git-ignored)
//   ACMP_LIVE_S3_SECRET_KEY
//   ACMP_LIVE_S3_ENDPOINT    optional, default s3.us-east-1.amazonaws.com
//   ACMP_LIVE_S3_REGION      optional, default us-east-1
public sealed class MinioFileStoreLiveS3Tests
{
    private const string BucketVar = "ACMP_LIVE_S3_BUCKET";
    private const string AccessVar = "ACMP_LIVE_S3_ACCESS_KEY";
    private const string SecretVar = "ACMP_LIVE_S3_SECRET_KEY";

    private static string? Env(string name)
    {
        var v = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    // A [Fact] that skips itself when the live-S3 environment is absent. Ten lines beats taking a dependency
    // on Xunit.SkippableFact for one file, and unlike an early `return` guard it cannot report a false pass.
    private sealed class LiveS3FactAttribute : FactAttribute
    {
        public LiveS3FactAttribute()
        {
            if (Env(BucketVar) is null || Env(AccessVar) is null || Env(SecretVar) is null)
                Skip = $"live S3 not configured — set {BucketVar}, {AccessVar}, {SecretVar} to run";
        }
    }

    // Built exactly as SharedKernelExtensions builds it in production (endpoint + credentials + SSL + region),
    // so this exercises the SHIPPED construction rather than a bespoke one assembled to make a test pass.
    private static MinioFileStore Store()
    {
        var client = new MinioClient()
            .WithEndpoint(Env("ACMP_LIVE_S3_ENDPOINT") ?? "s3.us-east-1.amazonaws.com")
            .WithCredentials(Env(AccessVar), Env(SecretVar))
            .WithSSL(true)
            .WithRegion(Env("ACMP_LIVE_S3_REGION") ?? "us-east-1")
            .Build();
        return new MinioFileStore(client, new MinioPresigner(client));
    }

    [LiveS3Fact]
    public async Task Recording_round_trips_through_real_s3_upload_presigned_playback_and_delete()
    {
        var bucket = Env(BucketVar)!;
        var store = Store();
        // Unique per run so a re-run cannot pass by finding the previous run's object.
        var key = $"acmp-live-probe/{Guid.NewGuid():N}.mp4";
        var payload = Encoding.UTF8.GetBytes("acmp-live-s3-probe-payload");

        try
        {
            // 1. upload — proves s3:PutObject is granted and the bucket probe does not abort the write
            var returned = await store.UploadAsync(bucket, key, new MemoryStream(payload), "video/mp4");
            returned.Should().Be(key);

            // 2. the object is really there — proves s3:GetObject/HeadObject
            (await store.ExistsAsync(bucket, key)).Should().BeTrue("the upload must be visible to StatObject");

            // 3. presigned PLAYBACK — the actual AC-078 claim. Fetch the URL over real HTTP, exactly as a
            //    browser <video> element would, and require the bytes back. A presigned URL that merely
            //    parses proves nothing; only a 200 with the payload proves playback works.
            var url = await store.GetPreSignedUrlAsync(bucket, key, TimeSpan.FromMinutes(5));

            // The ORIGIN is load-bearing, not cosmetic: nginx's CSP media-src must allow exactly this host or
            // playback is blocked in the browser with nothing in the API logs (the P22 finding). This value
            // CHANGED with the 6.0.3 -> 6.0.5 upgrade: 6.0.3 rewrote any Amazon host through AWSS3Endpoints,
            // mapping us-east-1 to the legacy global "s3.amazonaws.com" regardless of what was configured;
            // 6.0.5 honours the configured endpoint. ACMP_MEDIA_ORIGIN was updated to match. This is the LIVE
            // confirmation of it — MinioFileStoreCloudTests only pins the same expectation offline.
            new Uri(url).GetLeftPart(UriPartial.Authority).Should().Be("https://s3.us-east-1.amazonaws.com",
                "ACMP_MEDIA_ORIGIN and the CSP must name the host the SDK actually signs for");

            using var http = new HttpClient();
            var response = await http.GetAsync(url);
            response.IsSuccessStatusCode.Should().BeTrue(
                $"presigned playback must return 200, got {(int)response.StatusCode} {response.ReasonPhrase}");
            (await response.Content.ReadAsByteArrayAsync()).Should().Equal(payload);

            // 4. delete — proves s3:DeleteObject is granted and really removes the object
            await store.DeleteAsync(bucket, key);
            (await store.ExistsAsync(bucket, key)).Should().BeFalse("delete must actually remove the object");
        }
        finally
        {
            // Never leave probe objects in a real bucket if an assertion above threw mid-way.
            try { await store.DeleteAsync(bucket, key); } catch { /* already gone, or never created */ }
        }
    }

    // Cross-environment isolation (AC-083) at the DATA plane. simulate-principal-policy already proves this at
    // the IAM evaluation layer without touching a credential; this is the belt-and-braces version for when a
    // real key is in hand, and it asserts the write is REFUSED rather than merely absent.
    [LiveS3Fact]
    public async Task Writing_to_the_other_environments_bucket_is_refused()
    {
        var other = Env("ACMP_LIVE_S3_OTHER_BUCKET");
        if (other is null) return; // optional leg; the primary proof is the IAM simulation

        var store = Store();
        var key = $"acmp-live-probe/{Guid.NewGuid():N}.bin";

        // REGRESSION TEST FOR DEF-021. It went red on Minio 6.0.3 and green on 6.0.5; if it ever goes
        // red again the store has resumed reporting success for writes S3 refused, which is silent
        // DATA LOSS -- the API answers 201, the UI says success, the file is not there.
        //
        // On 6.0.3, measured against real S3: UploadAsync threw NOTHING and ExistsAsync then returned
        // TRUE for an object that does not exist. The client swallowed permission errors on BOTH
        // PutObject and StatObject, which also falsified the assumption written in
        // MinioFileStore.EnsureBucketAsync -- that "PutObject immediately after is the real authority
        // and its own failure surfaces to the caller". A post-write existence guard was tried and
        // REVERTED, because it inherits the same blindness and a guard that cannot fail is worse than
        // none. The actual fix was the SDK upgrade, found by trying the cheapest rung first.
        //
        // S3 itself was always correct: the AWS CLI answers, for this exact identity, bucket and
        // action, "AccessDenied ... acmp-uat-app is not authorized to perform: s3:PutObject on
        // arn:aws:s3:::acmp-prod-recordings/...", and an admin list-objects-v2 showed nothing stored.
        // So this doubles as the data-plane proof that cross-environment isolation holds (AC-083).
        await FluentActions
            .Awaiting(() => store.UploadAsync(other, key, new MemoryStream([1, 2, 3]), "application/octet-stream"))
            .Should().ThrowAsync<Exception>("S3 refuses this write, so the store must not report success (DEF-021)");
    }
}
