using Acmp.Shared.Infrastructure.FileStorage;
using FluentAssertions;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.Exceptions;
using NSubstitute;

namespace Acmp.Application.Tests.Storage;

/// <summary>
/// DEF-125 — MinioFileStore.ExistsAsync's behaviour when the SDK throws away the error.
///
/// ⚠⚠ WHY THESE ARE UNIT TESTS AND MinioFileStoreTests STAYS AN INTEGRATION TEST. The container suite
/// proves the adapter works against a real MinIO; it CANNOT force a transient 5xx, which is the whole
/// subject here. The failure it hit on CI run 33450264169 was unreproducible locally — four filtered runs
/// and one full parallel solution run all passed — so a substituted IMinioClient is the only instrument
/// that can drive this branch on demand. LL-013: a control nobody can force is a control nobody has tested.
///
/// THE BUG BEING GUARDED AGAINST IS UPSTREAM AND STILL OPEN. Minio.MinioClient.ParseErrorNoContent ends
/// with an unguarded `response.Exception.GetType()` — read at SDK tags 6.0.5 (pinned here) and 7.0.0
/// (latest), so an upgrade does not remove it. A body-less error response whose status is outside
/// {Forbidden, BadRequest, NotFound, MethodNotAllowed, NotImplemented} null-dereferences there, and
/// StatObject is a HEAD, so every error it sees is body-less.
/// </summary>
public class MinioFileStoreProbeTests
{
    private const string Bucket = "acmp-recordings";
    private const string Key = "mtg/one.mp4";

    private static MinioFileStore Store(IMinioClient client) => new(client, new MinioPresigner(client));

    private static IMinioClient ClientThrowing(params Exception[] perCall)
    {
        var client = Substitute.For<IMinioClient>();
        var calls = 0;
        client.StatObjectAsync(Arg.Any<StatObjectArgs>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var i = calls++;
                if (i < perCall.Length && perCall[i] is { } ex) throw ex;
                return Task.FromResult<ObjectStat>(null!);
            });
        return client;
    }

    [Fact] // The absent-object path must stay a plain false — the retry must not have changed it.
    public async Task An_absent_object_is_still_false_and_is_not_retried()
    {
        var client = ClientThrowing(new ObjectNotFoundException());

        (await Store(client).ExistsAsync(Bucket, Key)).Should().BeFalse();

        // ⚠ EXACTLY ONE CALL. A 404 is well-known to the SDK and never reaches the buggy line, so retrying
        // it would turn every miss into three round trips — and ExistsAsync("missing") is a hot path.
        await client.Received(1).StatObjectAsync(Arg.Any<StatObjectArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact] // The absent-bucket path, likewise.
    public async Task An_absent_bucket_is_still_false_and_is_not_retried()
    {
        var client = ClientThrowing(new BucketNotFoundException());

        (await Store(client).ExistsAsync(Bucket, Key)).Should().BeFalse();

        await client.Received(1).StatObjectAsync(Arg.Any<StatObjectArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact] // DEF-125's actual failure, recovering on the second attempt.
    public async Task A_transient_null_reference_from_the_SDK_is_retried_and_then_succeeds()
    {
        // One NRE, then the call succeeds — the shape of a single transient 5xx.
        var client = ClientThrowing(new NullReferenceException());

        (await Store(client).ExistsAsync(Bucket, Key)).Should().BeTrue();

        await client.Received(2).StatObjectAsync(Arg.Any<StatObjectArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact] // The exhaustion path: it must THROW, and it must never answer "absent".
    public async Task An_unrecoverable_probe_throws_a_named_exception_and_never_returns_false()
    {
        var client = ClientThrowing(
            new NullReferenceException(), new NullReferenceException(), new NullReferenceException());

        var act = () => Store(client).ExistsAsync(Bucket, Key);

        // ⛔ THE ASSERTION THAT MATTERS IS NOT THE TYPE, IT IS THAT NOTHING RETURNS false. An unreadable
        // answer from the object store is not evidence the object is absent, and converting it into one
        // would be a control that reassures while knowing nothing.
        var thrown = await act.Should().ThrowAsync<ObjectStoreProbeException>();
        thrown.Which.Bucket.Should().Be(Bucket);
        thrown.Which.ObjectName.Should().Be(Key);
        thrown.Which.Attempts.Should().Be(3);
        thrown.Which.InnerException.Should().BeOfType<NullReferenceException>();
        // The message has to carry the diagnosis, because the inner stack points into a third-party parser
        // and says nothing about ACMP at all.
        thrown.Which.Message.Should().Contain("DEF-125").And.Contain("does NOT mean the object is absent");

        await client.Received(3).StatObjectAsync(Arg.Any<StatObjectArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact] // The happy path, so the retry loop is proven not to have broken the ordinary answer.
    public async Task A_present_object_is_true_on_the_first_call()
    {
        var client = ClientThrowing();

        (await Store(client).ExistsAsync(Bucket, Key)).Should().BeTrue();

        await client.Received(1).StatObjectAsync(Arg.Any<StatObjectArgs>(), Arg.Any<CancellationToken>());
    }
}
