using Acmp.Modules.Dependencies.Domain;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Dependencies.Application.Abstractions;

// The narrow persistence port for the Dependencies module (schema "dependencies"). The EF implementation
// lives in Infrastructure and maps ONLY its own Dependency table (ADR-0001, docs/34 §12).
public interface IDependenciesDbContext
{
    DbSet<Dependency> Dependencies { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
