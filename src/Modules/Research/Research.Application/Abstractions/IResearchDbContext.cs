using Acmp.Modules.Research.Domain;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Research.Application.Abstractions;

// The Application layer talks to persistence through this narrow port; the EF implementation
// (schema "research") lives in Infrastructure and maps only its own tables (ADR-0001, docs/domain/repository-structure.md §12).
// Findings + Recommendations are owned collections of ResearchMission — EF loads/saves them with the aggregate.
public interface IResearchDbContext
{
    DbSet<ResearchMission> Missions { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
