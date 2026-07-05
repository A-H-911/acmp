using Acmp.Modules.Traceability.Domain;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Traceability.Application.Abstractions;

// The narrow persistence port for the Traceability module (schema "traceability"). The EF implementation
// lives in Infrastructure and maps ONLY its own Relationship table (ADR-0001, docs/domain/repository-structure.md §12).
public interface ITraceabilityDbContext
{
    DbSet<Relationship> Relationships { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
