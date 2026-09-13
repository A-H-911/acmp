using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Images;

namespace Acmp.Integration.Tests;

// WBS-41.2 (DEC-182 f2/f3, ADR-0047 e2). The FTS image is PULLED at the digest pinned in
// deploy/fts-test-image.json, not built: every build sampled a slow Ubuntu mirror, a context upload or an apt
// repository against a fixed budget (DEF-157, DEF-158, DEF-161). It is pulled only while
// deploy/Dockerfile.sqlserver still hashes to the pin's dockerfile_sha256; a changed file is a RED naming the
// per-bump step (DEC-182 f4), never a silent fallback to building. A deliberate candidate build
// (integration-on-candidate.yml) sets ACMP_FTS_BUILD=1 and gets the old path: built once from the Dockerfile.
// scripts/check-fts-pin.mjs makes the same comparison for the workflows.
//
// The pull runs through the docker CLI and the result is re-tagged locally, so Testcontainers boots a plain
// tag: the CLI uses the same credential store `docker login ghcr.io` fills, and the digest never goes through
// Testcontainers' own reference parser.
//
// DEF-161 still holds: whichever path runs, it runs AT MOST ONCE per test run. A failed pull or build is not
// retried by the next caller - the Lazy caches the faulted task, so every fixture fails with the same message
// (DEC-077 d3: a red is investigated, not re-sampled until it passes).
//
// ponytail: one image, one Lazy. A second Dockerfile would get its own holder, not a keyed registry.
internal static class FtsImage
{
    private const string Name = "SQL Server FTS (deploy/Dockerfile.sqlserver)";
    private const string PinnedTag = "acmp/sqlserver-fts:pinned";
    private const string BuildSwitch = "ACMP_FTS_BUILD";

    private static readonly string Root = CommonDirectoryPath.GetSolutionDirectory().DirectoryPath;

    private static readonly Lazy<Task<IImage>> Resolved = new(ResolveAsync);

    /// <summary>The FTS image, pulled (or, under ACMP_FTS_BUILD=1, built) on the first call; later and
    /// concurrent callers await that same task.</summary>
    public static Task<IImage> GetOnceAsync() => Resolved.Value;

    private static async Task<IImage> ResolveAsync()
    {
        if (Environment.GetEnvironmentVariable(BuildSwitch) == "1")
            return await BuildAsync();

        using var pin = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Root, "deploy", "fts-test-image.json")));
        var pinnedHash = pin.RootElement.GetProperty("dockerfile_sha256").GetString();
        var image = pin.RootElement.GetProperty("image").GetString()
            ?? throw new InvalidOperationException("deploy/fts-test-image.json has no image.");
        var actualHash = Convert.ToHexStringLower(
            SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(Root, "deploy", "Dockerfile.sqlserver"))));

        if (actualHash != pinnedHash)
            throw new InvalidOperationException(
                $"deploy/Dockerfile.sqlserver changed since the pinned FTS test image was published (sha256 {actualHash}, " +
                $"pin {pinnedHash}). Dispatch publish-fts-test-image.yml on this branch and commit the pin it prints " +
                $"(DEC-182 f4), or set {BuildSwitch}=1 to build this Dockerfile instead.");

        // Bounded by the build budget: a pull replaces the build it used to be, on the same path to the first
        // container, so it gets the same bound rather than a second number to keep in step (DW-085).
        await DockerAsync(ContainerStartup.BuildBudget, "pull", image);
        await DockerAsync(TimeSpan.FromSeconds(30), "tag", image, PinnedTag);
        Console.WriteLine($"[{Name}] pulled {image} (tagged {PinnedTag})"); // ADR-0047's confirmation: the log names the digest
        return new DockerImage(PinnedTag);
    }

    private static async Task<IImage> BuildAsync()
    {
        var image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(CommonDirectoryPath.GetSolutionDirectory(), "deploy")
            .WithDockerfile("Dockerfile.sqlserver")
            .WithName("acmp/sqlserver-fts:test")
            .WithCleanUp(false) // keep the built image cached across runs
                                // DEF-140: without the logger the build is SILENT, and BuildOrFailFastAsync's
                                // timeout message would have no build output to embed.
            .WithLogger(DockerBuildLog.Instance)
            .Build();
        await ContainerStartup.BuildOrFailFastAsync(image, Name);
        Console.WriteLine($"[{Name}] built from the Dockerfile ({BuildSwitch}=1)");
        return image;
    }

    private static async Task DockerAsync(TimeSpan bound, params string[] args)
    {
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args)
            start.ArgumentList.Add(arg);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("could not start the docker CLI");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var cts = new CancellationTokenSource(bound);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"docker {string.Join(' ', args)} did not finish in {bound.TotalSeconds:0} seconds.{Environment.NewLine}{await stderr}");
        }

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"docker {string.Join(' ', args)} failed (exit {process.ExitCode}). A pull of the private package needs " +
                $"`docker login ghcr.io` (tests/Acmp.Integration.Tests/README.md).{Environment.NewLine}{await stderr}{await stdout}");
    }
}
