using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Audit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acmp.Application.Tests.Audit;

// D-16 / C-INS-02 (ADR-0030) — the nightly integrity verifier's alert routing, and the audit store's own
// chain check. The verifier must emit a durable AuditEvent (the tripwire) for every failure and for a check
// that throws, and stay silent on a clean sweep.
[Trait("Category", "Security")]
public class IntegrityVerifierTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private sealed class RecordingSink : IAuditSink
    {
        public List<string> Events { get; } = new();

        public Task EmitAsync(string eventType, string? subject, object? data = null, CancellationToken ct = default)
        {
            Events.Add(eventType);
            return Task.CompletedTask;
        }

        public Task EmitEnrichedAsync(string action, string subjectType, string? subjectId,
            string outcome = AuditOutcome.Success, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeCheck : IIntegrityCheck
    {
        private readonly Func<IntegrityCheckResult> _run;
        public FakeCheck(string name, Func<IntegrityCheckResult> run) { Name = name; _run = run; }
        public string Name { get; }
        public Task<IntegrityCheckResult> RunAsync(CancellationToken ct = default) => Task.FromResult(_run());
    }

    private sealed class ThrowingCheck : IIntegrityCheck
    {
        public string Name => "throwing";
        public Task<IntegrityCheckResult> RunAsync(CancellationToken ct = default) =>
            throw new InvalidOperationException("audit store unreachable");
    }

    [Fact]
    public async Task Alerts_and_audits_each_failure_leaving_clean_checks_silent()
    {
        var sink = new RecordingSink();
        var verifier = new IntegrityVerifier(new IIntegrityCheck[]
        {
            new FakeCheck("audit-chain", () => IntegrityCheckResult.Ok("audit-chain", 5)),
            new FakeCheck("vote-ballot-chain", () => IntegrityCheckResult.Broken("vote-ballot-chain", 3, "VOTE-1 ballot 2")),
            new ThrowingCheck(),
        }, sink, NullLogger<IntegrityVerifier>.Instance);

        var result = await verifier.RunAsync();

        result.ChecksRun.Should().Be(3);
        result.Failures.Should().Be(2);
        sink.Events.Should().BeEquivalentTo("Security.IntegrityBreachDetected", "Security.IntegrityCheckError");
    }

    [Fact]
    public async Task A_fully_intact_sweep_emits_no_audit_rows()
    {
        var sink = new RecordingSink();
        var verifier = new IntegrityVerifier(new IIntegrityCheck[]
        {
            new FakeCheck("audit-chain", () => IntegrityCheckResult.Ok("audit-chain", 2)),
        }, sink, NullLogger<IntegrityVerifier>.Instance);

        var result = await verifier.RunAsync();

        result.Failures.Should().Be(0);
        sink.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Audit_chain_check_reports_ok_intact_then_broken_after_a_direct_edit()
    {
        var opts = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var e1 = AuditEvent.CreateNext(AuditEvent.Genesis, Now, "Test.One", "s", null);
        var e2 = AuditEvent.CreateNext(e1.Hash, Now, "Test.Two", "s", null);
        await using (var db = new AuditDbContext(opts))
        {
            db.AuditEvents.AddRange(e1, e2);
            await db.SaveChangesAsync();
        }

        await using (var db = new AuditDbContext(opts))
        {
            var ok = await new AuditChainIntegrityCheck(db).RunAsync();
            ok.IsValid.Should().BeTrue();
            ok.Scanned.Should().Be(2);
        }

        // Simulate a DBA/direct-SQL content edit: change a stored field so Recompute() != Hash.
        await using (var db = new AuditDbContext(opts))
        {
            var row = await db.AuditEvents.OrderBy(e => e.Sequence).FirstAsync();
            typeof(AuditEvent).GetProperty("EventType",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance)!.SetValue(row, "TAMPERED");
            await db.SaveChangesAsync();
        }

        await using (var db = new AuditDbContext(opts))
        {
            var broken = await new AuditChainIntegrityCheck(db).RunAsync();
            broken.IsValid.Should().BeFalse();
            broken.FirstFailure.Should().Contain("tampered");
        }
    }
}
