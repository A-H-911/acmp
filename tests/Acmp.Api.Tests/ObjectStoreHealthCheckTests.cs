using Acmp.Api.Infrastructure;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Acmp.Api.Tests;

/*
 * DEF-078 / DEC-056 d1. The check this replaces was [ExcludeFromCodeCoverage] and had NO tests, and it shipped
 * in a state where it could not pass on the environment it ran in — /readyz was measured at 503 on production
 * for three days. These tests exist so that cannot recur silently: the point of moving to an SDK call is that
 * IMinioClient is an interface, so every branch is assertable without standing up MinIO or S3.
 */
public sealed class ObjectStoreHealthCheckTests
{
    private static ObjectStoreHealthCheck Create(IMinioClient client, string recordings = "acmp-recordings",
        string attachments = "acmp-topics") =>
        new(client, Options.Create(new StorageOptions
        {
            RecordingsBucket = recordings,
            AttachmentsBucket = attachments,
        }));

    private static HealthCheckContext Context() => new();

    [Fact]
    public async Task Reports_healthy_when_every_configured_bucket_exists()
    {
        var client = Substitute.For<IMinioClient>();
        client.BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await Create(client).CheckHealthAsync(Context());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Reports_unhealthy_when_a_bucket_does_not_exist()
    {
        var client = Substitute.For<IMinioClient>();
        client.BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await Create(client).CheckHealthAsync(Context());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task Reports_unhealthy_when_the_client_throws()
    {
        var client = Substitute.For<IMinioClient>();
        client.BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("connection refused"));

        var result = await Create(client).CheckHealthAsync(Context());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    /*
     * THE "TELL" HALF, ASSERTED RATHER THAN ASSUMED. A control that detects but does not say WHAT it detected
     * is the failure this project has recorded nine times. The measured production response body was the bare
     * word "Unhealthy", which sent someone to read source to find out which dependency was down.
     */
    [Fact]
    public async Task Names_the_offending_bucket_and_the_reason_in_its_description()
    {
        var client = Substitute.For<IMinioClient>();
        client.BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await Create(client, recordings: "acmp-prod-recordings", attachments: "acmp-prod-recordings")
            .CheckHealthAsync(Context());

        result.Description.Should().Contain("acmp-prod-recordings");
        result.Description.Should().Contain("does not exist or is not visible");
    }

    [Fact]
    public async Task Names_the_bucket_when_the_client_throws()
    {
        var client = Substitute.For<IMinioClient>();
        client.BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("connection refused"));

        var result = await Create(client, recordings: "acmp-uat-bucket", attachments: "acmp-uat-bucket")
            .CheckHealthAsync(Context());

        result.Description.Should().Contain("acmp-uat-bucket");
        result.Description.Should().Contain("connection refused");
    }

    /*
     * A check with nothing to probe MUST NOT report Healthy. This is the vacuous-pass guard: DEF-056's
     * NotContain controls were green from the day they were written because they asserted over an empty set,
     * and a health check that finds no buckets configured and says "fine" is the same shape.
     */
    [Fact]
    public async Task Reports_unhealthy_rather_than_vacuously_healthy_when_no_bucket_is_configured()
    {
        var client = Substitute.For<IMinioClient>();

        var result = await Create(client, recordings: "", attachments: "   ").CheckHealthAsync(Context());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("no object-storage buckets configured");
        await client.DidNotReceive().BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>());
    }

    /*
     * On cloud both names are set to the SAME per-environment bucket (Storage__RecordingsBucket and
     * Storage__AttachmentsBucket both take ${ACMP_S3_BUCKET}), so the check must not pay for two round trips
     * to answer one question.
     */
    [Fact]
    public async Task Probes_a_shared_bucket_once_not_twice()
    {
        var client = Substitute.For<IMinioClient>();
        client.BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>()).Returns(true);

        await Create(client, recordings: "acmp-prod", attachments: "acmp-prod").CheckHealthAsync(Context());

        await client.Received(1).BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Probes_both_buckets_when_they_differ()
    {
        var client = Substitute.For<IMinioClient>();
        client.BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>()).Returns(true);

        await Create(client, recordings: "acmp-recordings", attachments: "acmp-topics").CheckHealthAsync(Context());

        await client.Received(2).BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>());
    }

    /*
     * ⚠ THE REGRESSION GUARD FOR DEF-078 ITSELF, and it is the reason this file exists. The old check GET a
     * MinIO-daemon path derived from Minio:Endpoint, so pointing the app at S3 broke readiness while every
     * test stayed green. Asserting that the probe is driven by the CONFIGURED BUCKET NAME — not by an endpoint
     * URL, a scheme, or a hard-coded path — is what makes "works on MinIO and S3 unchanged" a property of the
     * code rather than a claim in a comment.
     */
    [Fact]
    public async Task Probes_the_configured_bucket_name_regardless_of_endpoint_flavour()
    {
        var client = Substitute.For<IMinioClient>();
        BucketExistsArgs? captured = null;
        client.BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>())
            .Returns(call => { captured = call.Arg<BucketExistsArgs>(); return true; });

        await Create(client, recordings: "acmp-prod-recordings", attachments: "acmp-prod-recordings")
            .CheckHealthAsync(Context());

        captured.Should().NotBeNull();

        /*
         * BucketName is not public on Minio 6.0.5's BucketExistsArgs, so it is read reflectively. ⚠ THE
         * INSTRUMENT IS PROVEN PRESENT FIRST, DELIBERATELY: if a future SDK renames the member, a null lookup
         * would silently compare null to null and this guard would pass while measuring nothing — which is the
         * exact failure this test exists to prevent elsewhere. Assert the accessor exists, THEN assert on it.
         */
        var member = typeof(BucketExistsArgs).GetProperty("BucketName",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic);
        member.Should().NotBeNull("the Minio SDK must still expose BucketName, or this guard measures nothing");

        member!.GetValue(captured).Should().Be("acmp-prod-recordings");
    }

    /*
     * A readiness endpoint that HANGS is worse than one that says no: callers time out at their own layer with
     * no reason attached. The HTTP probe this replaces capped itself at 3s and that cap must not be lost.
     */
    [Fact]
    public async Task Fails_rather_than_hangs_when_the_object_store_never_answers()
    {
        var client = Substitute.For<IMinioClient>();
        client.BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>())
            .Returns(async call => await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>())
                .ContinueWith(_ => true, TaskContinuationOptions.OnlyOnRanToCompletion));

        var result = await Create(client).CheckHealthAsync(Context());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("timed out");
    }

    /*
     * A caller-initiated cancellation is NOT a storage fault and must not be reported as one — otherwise a
     * shutdown or a client disconnect would be recorded as an object-store outage.
     */
    [Fact]
    public async Task Propagates_caller_cancellation_instead_of_blaming_the_object_store()
    {
        var client = Substitute.For<IMinioClient>();
        using var cts = new CancellationTokenSource();
        client.When(c => c.BucketExistsAsync(Arg.Any<BucketExistsArgs>(), Arg.Any<CancellationToken>()))
            .Do(_ => { cts.Cancel(); throw new OperationCanceledException(cts.Token); });

        var act = async () => await Create(client).CheckHealthAsync(Context(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
