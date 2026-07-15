using Acmp.Shared.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Acmp.Shared.Infrastructure.Audit;

// D-16 / C-INS-02 (ADR-0030) — runs every registered IIntegrityCheck and routes any failure to ONE alert
// path: a high-importance Serilog/Seq event plus a durable AuditEvent (the detection is itself tamper-evident,
// and extends the very chain that was checked). Isolated per check: one broken chain is reported and audited,
// the others still run.
public sealed class IntegrityVerifier : IIntegrityVerifier
{
    private const string SystemActor = "system:integrity-verify";

    private readonly IReadOnlyList<IIntegrityCheck> _checks;
    private readonly IAuditSink _audit;
    private readonly ILogger<IntegrityVerifier> _logger;

    public IntegrityVerifier(IEnumerable<IIntegrityCheck> checks, IAuditSink audit, ILogger<IntegrityVerifier> logger)
    {
        _checks = checks.ToList();
        _audit = audit;
        _logger = logger;
    }

    public async Task<IntegritySweepResult> RunAsync(CancellationToken ct = default)
    {
        var failures = 0;

        foreach (var check in _checks)
        {
            IntegrityCheckResult result;
            try
            {
                result = await check.RunAsync(ct);
            }
            catch (Exception ex)
            {
                // A check that cannot even run is itself a signal (e.g. the audit store is unreachable, NFR-046).
                failures++;
                _logger.LogError(ex, "INTEGRITY ALERT: check {Check} failed to run", check.Name);
                await _audit.EmitAsync("Security.IntegrityCheckError", SystemActor,
                    new { check.Name, Error = ex.Message }, ct);
                continue;
            }

            if (result.IsValid)
            {
                _logger.LogInformation("Integrity check {Check} OK ({Scanned} scanned)", result.Name, result.Scanned);
            }
            else
            {
                failures++;
                _logger.LogError("INTEGRITY ALERT: {Check} detected tampering at {Failure} ({Scanned} scanned)",
                    result.Name, result.FirstFailure, result.Scanned);
                await _audit.EmitAsync("Security.IntegrityBreachDetected", SystemActor,
                    new { result.Name, result.FirstFailure, result.Scanned }, ct);
            }
        }

        return new IntegritySweepResult(_checks.Count, failures);
    }
}
