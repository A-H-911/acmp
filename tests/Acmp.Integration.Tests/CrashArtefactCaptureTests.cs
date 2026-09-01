using DotNet.Testcontainers.Builders;
using FluentAssertions;

namespace Acmp.Integration.Tests;

// DEF-121's instrument, and like every other guard here it is proved by FORCING its subject rather than
// by a green run. The row's clause (2) asks for "a captured log that identifies a cause"; the log was
// already captured on the recorded occurrence and identified none, while `/var/opt/mssql/log` — errorlog,
// the SQLPAL stack dump, the core file — died with the container. So the clause was waiting on an
// experiment whose instrument could not answer it, which is LL-047's shape pointed at our own end
// condition. These tests prove the instrument now answers.
//
// ⚠ The forced container CRASHES rather than hangs, because that is DEF-121's actual shape: it exited
// with code 1 and Testcontainers threw ContainerNotRunningException — a path StartOrFailFastAsync used to
// let straight through without capturing anything. Requires a running Docker daemon.
public sealed class CrashArtefactCaptureTests
{
    private const string CrashMarker = "acmp-forced-crash-marker";
    private const string NeverLogged = "acmp-message-that-is-never-logged";

    private static readonly TimeSpan ForcedBudget = TimeSpan.FromSeconds(60);

    // Writes a stand-in for what handle-crash.sh leaves behind, then exits non-zero. The wait strategy
    // names a message the container never logs, so Testcontainers polls, finds the container gone, and
    // raises the crash — rather than the budget expiring, which would exercise the timeout path instead.
    private static ContainerBuilder CrashingContainer() => new ContainerBuilder("alpine:3.20")
        .WithEntrypoint("/bin/sh", "-c")
        .WithCommand($"mkdir -p /var/opt/mssql/log && echo {CrashMarker} > /var/opt/mssql/log/errorlog && exit 1")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged(NeverLogged));

    [Fact]
    public async Task A_container_that_crashes_at_startup_has_its_log_directory_lifted_out()
    {
        var destination = Directory.CreateTempSubdirectory("acmp-crash-artefacts-");
        var previous = Environment.GetEnvironmentVariable("ACMP_CRASH_ARTEFACT_DIR");
        Environment.SetEnvironmentVariable("ACMP_CRASH_ARTEFACT_DIR", destination.FullName);

        try
        {
            await using var crashed = CrashingContainer().Build();

            var start = () => ContainerStartup.StartOrFailFastAsync(crashed, "forced-crash", ForcedBudget);

            await start.Should().ThrowAsync<Exception>(
                "a container that exits non-zero must still fail the test that needed it");

            // ⚠⚠ THE ASSERTION THAT MATTERS IS THE CONTENT, NOT THE DIRECTORY. LL-041: a file count proves
            // the copy ran, never that it had a subject — an empty directory and a successful capture of an
            // empty log look identical from outside. This asserts the container's OWN bytes came out.
            var captured = Directory.GetFiles(destination.FullName, "errorlog", SearchOption.AllDirectories);

            captured.Should().ContainSingle(
                "the crashed container's /var/opt/mssql/log must be lifted out before anything disposes it — "
                + "on DEF-121's recorded occurrence this directory, including the core dump handle-crash.sh "
                + "referenced by path, was unretrievable from the run");

            (await File.ReadAllTextAsync(captured[0])).Should().Contain(CrashMarker,
                "the captured file must carry the container's own content; an empty capture reads exactly "
                + "like a successful one and would leave clause (2) as unreachable as it was before");

            var manifest = Directory.GetFiles(destination.FullName, "manifest.txt", SearchOption.AllDirectories);

            manifest.Should().ContainSingle("whoever downloads the artifact weeks later has to be able to "
                + "tell a capture that found nothing from one whose outsized files were dropped");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ACMP_CRASH_ARTEFACT_DIR", previous);
            destination.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Capturing_from_a_container_that_was_never_created_says_so_instead_of_throwing()
    {
        // The budget can expire during the image pull, before any container exists. This path runs while
        // the test is ALREADY failing, so it must never replace the real exception with its own — and it
        // must SAY that nothing was captured rather than returning a sentence that reads like a capture
        // (nine instances in this project of a control that detects and does not tell).
        var destination = Directory.CreateTempSubdirectory("acmp-crash-artefacts-none-");

        try
        {
            await using var neverStarted = new ContainerBuilder("alpine:3.20").Build();

            var message = await CrashArtefacts.CaptureAsync(
                neverStarted, "never-started", destinationRoot: destination.FullName);

            message.Should().Contain("nothing to capture",
                "an absent container and an empty log directory are different findings and must read differently");

            Directory.GetFileSystemEntries(destination.FullName).Should().BeEmpty(
                "no container existed, so nothing may be written — an empty capture directory is the honest artifact");
        }
        finally
        {
            destination.Delete(recursive: true);
        }
    }
}
