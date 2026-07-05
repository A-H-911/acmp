using Acmp.Modules.Governance.Domain;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Governance.Application.Abstractions;

// The Application layer talks to persistence through this narrow port; the EF implementation
// (schema "governance") lives in Infrastructure and maps only its own tables (ADR-0001, docs/domain/repository-structure.md §12).
// Considered options are an owned collection of Adr — EF loads/saves them with the aggregate.
public interface IGovernanceDbContext
{
    DbSet<Adr> Adrs { get; }
    DbSet<Invariant> Invariants { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
