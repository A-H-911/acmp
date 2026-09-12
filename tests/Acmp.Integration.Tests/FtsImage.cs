using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Images;

namespace Acmp.Integration.Tests;

// DEF-161. The FTS image (deploy/Dockerfile.sqlserver, ~4 GB) is built AT MOST ONCE per test run, however
// many fixtures boot a container from it. It used to be built once per IAsyncLifetime initialisation:
// NfrPerfFixture once and SearchProvidersFtsTests once PER TEST, so three builds per run. PR #401's backend
// job (run 34599300375) showed the cost: two builds finished, a third ran cold and spent its whole 480 s
// budget fetching Ubuntu indices at 91 kB/s. Every extra build was another sample of a slow mirror against
// that bound; one build is one sample.
//
// A failed build is NOT retried by the next caller: the Lazy caches the faulted task, so every fixture that
// needs the image fails with the same legible TimeoutException from BuildOrFailFastAsync. That is the point
// of DEC-077 d3 - a red is investigated, not re-sampled until it passes.
//
// ponytail: one image, one Lazy. A second Dockerfile would get its own holder, not a keyed registry.
internal static class FtsImage
{
    private const string Name = "SQL Server FTS (deploy/Dockerfile.sqlserver)";

    private static readonly IFutureDockerImage Image = new ImageFromDockerfileBuilder()
        .WithDockerfileDirectory(CommonDirectoryPath.GetSolutionDirectory(), "deploy")
        .WithDockerfile("Dockerfile.sqlserver")
        .WithName("acmp/sqlserver-fts:test")
        .WithCleanUp(false) // keep the built image cached across runs
                            // DEF-140: without the logger the build is SILENT, and BuildOrFailFastAsync's
                            // timeout message would have no build output to embed.
        .WithLogger(DockerBuildLog.Instance)
        .Build();

    private static readonly Lazy<Task> Built = new(() => ContainerStartup.BuildOrFailFastAsync(Image, Name));

    /// <summary>The FTS image, built on the first call; later and concurrent callers await that same build.</summary>
    public static async Task<IFutureDockerImage> BuildOnceAsync()
    {
        await Built.Value;
        return Image;
    }
}
