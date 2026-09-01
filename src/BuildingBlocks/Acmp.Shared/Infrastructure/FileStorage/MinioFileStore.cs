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

    // DEF-125. THE PROBE RETRIES BECAUSE THE SDK DESTROYS THE ERROR, AND THAT IS THE WHOLE REASON.
    // Minio.MinioClient.ParseErrorNoContent ends with `response.Exception.GetType()` and NEVER null-checks
    // it — verified in the SDK's own source at tag 6.0.5, which this project pins, AND at 7.0.0, the latest
    // release, so upgrading does not fix it. Its two earlier branches both call ParseWellKnownErrorNoContent
    // (which throws) for exactly five statuses: Forbidden, BadRequest, NotFound, MethodNotAllowed,
    // NotImplemented. An error response outside that set, with NO BODY and no transport exception, falls
    // past them and null-dereferences — where the SDK's own next line meant to throw a descriptive
    // InternalClientException. StatObject is a HEAD, so every error response it receives is body-less by
    // construction, which is why this probe is where the bug surfaces.
    //
    // So a transient 5xx from the object store arrives here as a bare NullReferenceException carrying
    // nothing: no status, no bucket, no object. A 404 never reaches it — that is well-known and throws
    // ObjectNotFoundException — which is why this path passed on every CI run but one before DEF-125.
    //
    // ⛔ RETRYING IS NOT A PROBABILITY-FUDGE (LL-035) AND `return false` IS NOT AN OPTION. The probe is an
    // idempotent HEAD, so asking again is free of side effects and is the ONLY way left to distinguish a
    // transient condition from a persistent one once the SDK has thrown the detail away. And "the store
    // could not tell me" is emphatically not "the object is absent": returning false here would convert an
    // outage into a confident wrong answer — the DEF-023/051/054/078 family, on a path that decides whether
    // a recording exists.
    private const int ProbeAttempts = 3;

    public async Task<bool> ExistsAsync(string bucket, string objectName, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await _client.StatObjectAsync(new StatObjectArgs().WithBucket(bucket).WithObject(objectName), ct);
                return true;
            }
            catch (ObjectNotFoundException) { return false; }
            catch (BucketNotFoundException) { return false; }
            catch (NullReferenceException ex)
            {
                // Narrow ON PURPOSE: this is the one signature DEF-125 diagnosed, not a guess at which
                // failures are transient. Widening it to `Exception` would swallow real faults, and adding
                // speculative SDK types would be unmeasured.
                if (attempt >= ProbeAttempts)
                    throw new ObjectStoreProbeException(bucket, objectName, attempt, ex);

                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), ct);
            }
        }
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

// DEF-125. What the SDK's null-dereference threw away, said out loud: which bucket, which object, how many
// attempts, and the original exception as InnerException. The message names the upstream bug explicitly,
// because the next person to see this will otherwise be reading a NullReferenceException stack that points
// into a third-party parser and says nothing about ACMP at all.
//
// ⚠ THIS TYPE EXISTS SO THE FAILURE IS LEGIBLE, NOT SO CALLERS CAN BRANCH ON IT. Nothing catches it today
// and nothing should: an unreadable answer from the object store is a real failure and belongs at the top
// of the call stack, not converted into a boolean by whoever is nearest.
public sealed class ObjectStoreProbeException : Exception
{
    public ObjectStoreProbeException(string bucket, string objectName, int attempts, Exception inner)
        : base($"Object store probe for '{objectName}' in bucket '{bucket}' failed on all {attempts} attempts. "
               + "The MinIO SDK raised a NullReferenceException from ParseErrorNoContent, which is its "
               + "unguarded `response.Exception.GetType()` (present in 6.0.5 and 7.0.0) — it means the store "
               + "returned an error with no body and a status outside the five it treats as well-known, "
               + "typically a transient 5xx. It does NOT mean the object is absent. See DEF-125.", inner)
    {
        Bucket = bucket;
        ObjectName = objectName;
        Attempts = attempts;
    }

    public string Bucket { get; }

    public string ObjectName { get; }

    public int Attempts { get; }
}

// Holds the IMinioClient used for presigning — the public-endpoint client when configured (browser-reachable
// via nginx), else the internal client. A distinct singleton so upload/exists/delete stay on the fast internal
// endpoint.
//
// ⚠ THE REASON THIS CLASS GIVES FOR LIVING HERE WAS STALE AND IS CORRECTED (DEF-125's PR, DEC-105 d2's
// rider shape): it said "so it inherits the MinioFileStore coverage exclusion (ADR-0016 §1)", and that
// exclusion NO LONGER EXISTS — coverlet.runsettings says so in its own words, because MinioFileStore is the
// production recording store and is covered end-to-end by MinioFileStoreTests. A comment naming a rule that
// was removed is the class DEF-094 and DW-091 both cost this project a build over.
public sealed class MinioPresigner
{
    public MinioPresigner(IMinioClient client) => Client = client;

    public IMinioClient Client { get; }
}
