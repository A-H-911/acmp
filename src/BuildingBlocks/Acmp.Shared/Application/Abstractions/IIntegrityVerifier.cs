namespace Acmp.Shared.Application.Abstractions;

// D-16 / C-INS-02 (ADR-0030) — the nightly integrity tripwire. Runs every registered IIntegrityCheck and, on
// any detected chain gap (or a check that throws), emits a high-importance Serilog/Seq event AND a durable
// AuditEvent (so the detection is itself tamper-evident). Cron-triggered by Acmp.Worker; a plain service (not
// a MediatR command) because it takes no input, needs no auth/validation pipeline, and must not depend on
// MediatR scanning Acmp.Shared.
public interface IIntegrityVerifier
{
    Task<IntegritySweepResult> RunAsync(CancellationToken ct = default);
}

public sealed record IntegritySweepResult(int ChecksRun, int Failures);
