using Acmp.Shared.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Shared.Infrastructure.Persistence;

// NFR-042 (ADR-0026). One line every module (and the audit context) adds to its AddDbContext so the before/after
// capture + same-transaction atomicity interceptors are attached. These are added EXPLICITLY, not via EF's DI
// auto-apply of registered IInterceptors — that auto-apply does not fire for these contexts (proven by
// AuditAtomicityTests: without the explicit call the transaction is never begun and a failed audit append does
// not roll the state change back). The interceptors are registered as concrete scoped types in AddSharedKernel.
public static class SharedDbContextOptions
{
    public static DbContextOptionsBuilder AddAcmpAuditInterceptors(
        this DbContextOptionsBuilder options, IServiceProvider sp) =>
        options.AddInterceptors(
            sp.GetRequiredService<AuditCaptureInterceptor>(),
            sp.GetRequiredService<AmbientTransactionStarter>(),
            sp.GetRequiredService<AmbientTransactionInterceptor>());
}
