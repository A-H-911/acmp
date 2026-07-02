using Acmp.Modules.Risks.Domain;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Risks.Application.Abstractions;

// The Application layer talks to persistence through this narrow port; the EF implementation
// (schema "risks") lives in Infrastructure and maps only its own tables (ADR-0001, docs/34 §12).
// Mitigations are an owned collection of Risk — EF loads/saves them with the aggregate (no separate set).
public interface IRisksDbContext
{
    DbSet<Risk> Risks { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
