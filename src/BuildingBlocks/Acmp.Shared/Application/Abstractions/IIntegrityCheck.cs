namespace Acmp.Shared.Application.Abstractions;

// D-16 / C-INS-02 (ADR-0030) — a module's self-contained tamper check, run by the nightly integrity verifier.
// Each module verifies ONLY its own aggregate (no cross-module table reads, ADR-0001); the verifier fans out
// over every registered check and routes any failure to one alert path. Adding a future module's chain is a
// new IIntegrityCheck registration — no change to the verifier.
public interface IIntegrityCheck
{
    // Stable identifier used in logs/audit (e.g. "audit-chain", "vote-ballot-chain").
    string Name { get; }

    Task<IntegrityCheckResult> RunAsync(CancellationToken ct = default);
}

// One check's outcome. Scanned = rows examined (for the OK log line); FirstFailure = a human-readable pointer
// to the first break (null when valid).
public sealed record IntegrityCheckResult(string Name, bool IsValid, int Scanned, string? FirstFailure)
{
    public static IntegrityCheckResult Ok(string name, int scanned) => new(name, true, scanned, null);

    public static IntegrityCheckResult Broken(string name, int scanned, string firstFailure) =>
        new(name, false, scanned, firstFailure);
}
