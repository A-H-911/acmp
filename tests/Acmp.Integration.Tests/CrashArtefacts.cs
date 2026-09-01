using System.Diagnostics;
using DotNet.Testcontainers.Containers;

namespace Acmp.Integration.Tests;

// DEF-121 clause (2) — "the failure fires again and the captured container log identifies a cause".
// On the recorded occurrence it could not, and the reason was structural rather than bad luck:
// Testcontainers surfaced the container's stdout/stderr, which named an AppLoader status and no cause,
// while everything that might have named one — `/var/opt/mssql/log`'s errorlog, the SQLPAL stack dump
// `handle-crash.sh` writes, and the core file it references by path — died inside the container when it
// was disposed. The row records the dump as "NOT retrievable from the run".
//
// So the clause was waiting on an experiment whose instrument could not answer it. This lifts that
// directory out while the exited container still exists, so the clause has something to fire on.
//
// ⚠ THIS IMPROVES AN INSTRUMENT. It does not reduce the probability of the fault (LL-035), it diagnoses
// nothing, and it closes no row — DEF-121 stays Open at high severity until one of its clauses is met.
//
// ⚠ `docker cp` rather than IContainer.ReadFileAsync, and the reason is the core file's NAME: it is
// timestamped (`core.sqlservr.8_30_2026_6_49_31.20`), so it cannot be requested by path in advance, and
// ReadFileAsync takes one known path. `docker cp` takes the directory and does not need to know.
internal static class CrashArtefacts
{
    /// <summary>Where captures land. CI points this at a workspace path it uploads on failure.</summary>
    internal static string Directory =>
        Environment.GetEnvironmentVariable("ACMP_CRASH_ARTEFACT_DIR")
        ?? Path.Combine(Path.GetTempPath(), "acmp-container-crash");

    // A SQL Server core file is gigabytes and needs matching symbols to read; the TEXT beside it is what
    // names a cause. Outsized files are dropped so the artifact stays uploadable — but they are NAMED in
    // the manifest, because an artifact that silently omits the biggest file reads exactly like one that
    // never had it (controls must detect AND tell).
    private const long MaxFileBytes = 64L * 1024 * 1024;

    private static readonly TimeSpan CopyBudget = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Copy <paramref name="sourcePath"/> out of a container that failed to start. Never throws: this runs
    /// on a path that is already failing, and a diagnostic that replaces the real exception with its own is
    /// worse than no diagnostic. Returns a sentence for the caller to attach to that exception.
    /// </summary>
    public static async Task<string> CaptureAsync(
        IContainer container,
        string name,
        string sourcePath = "/var/opt/mssql/log",
        string? destinationRoot = null)
    {
        string id;
        try
        {
            id = container.Id;
        }
        catch (Exception ex)
        {
            // The budget can expire during the image pull, before any container exists. Say WHICH —
            // "nothing was captured" and "there was nothing to capture" are different findings.
            return $"(no container exists yet, so there is nothing to capture: {ex.GetType().Name})";
        }

        if (string.IsNullOrEmpty(id))
            return "(no container exists yet, so there is nothing to capture)";

        var shortId = id[..Math.Min(12, id.Length)];
        var dest = Path.Combine(destinationRoot ?? Directory, $"{Slug(name)}-{shortId}");

        try
        {
            System.IO.Directory.CreateDirectory(dest);

            var (ok, output) = await DockerCpAsync(id, sourcePath, dest);
            if (!ok)
                return $"(no crash artefacts were captured — `docker cp {shortId}:{sourcePath}` failed: {output})";

            var manifest = WriteManifest(dest, sourcePath, shortId);
            return $"Container crash artefacts captured to {dest} — {manifest}";
        }
        catch (Exception ex)
        {
            return $"(capturing crash artefacts FAILED: {ex.GetType().Name}: {ex.Message})";
        }
    }

    private static async Task<(bool Ok, string Output)> DockerCpAsync(string id, string sourcePath, string dest)
    {
        var psi = new ProcessStartInfo("docker", $"cp {id}:{sourcePath} \"{dest}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi);
        if (process is null) return (false, "the docker CLI could not be started");

        using var cts = new CancellationTokenSource(CopyBudget);
        var stdout = await process.StandardOutput.ReadToEndAsync(cts.Token);
        var stderr = await process.StandardError.ReadToEndAsync(cts.Token);
        await process.WaitForExitAsync(cts.Token);

        return (process.ExitCode == 0, string.Concat(stdout, stderr).Trim());
    }

    // Drops outsized files and records what was kept and what was dropped, next to the files themselves —
    // so the artifact explains its own contents to whoever downloads it weeks later.
    private static string WriteManifest(string dest, string sourcePath, string shortId)
    {
        var lines = new List<string>
        {
            $"container {shortId}, copied from {sourcePath} at {DateTimeOffset.UtcNow:O}",
            $"files over {MaxFileBytes / 1048576} MB are DROPPED here and listed below — they were present in the container",
            "",
        };

        var kept = 0;
        var dropped = 0;

        foreach (var file in System.IO.Directory.GetFiles(dest, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            var relative = Path.GetRelativePath(dest, file);
            if (info.Length > MaxFileBytes)
            {
                lines.Add($"DROPPED {relative} ({info.Length / 1048576} MB)");
                File.Delete(file);
                dropped++;
            }
            else
            {
                lines.Add($"kept    {relative} ({info.Length} bytes)");
                kept++;
            }
        }

        if (kept == 0 && dropped == 0)
            lines.Add("(the directory existed but was EMPTY)");

        File.WriteAllLines(Path.Combine(dest, "manifest.txt"), lines);
        return $"{kept} file(s) kept, {dropped} dropped as outsized (see manifest.txt)";
    }

    private static string Slug(string name) =>
        string.Concat(name.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-'));
}
