using Acmp.Modules.Decisions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Decisions.Application.Abstractions;

// The Application layer talks to persistence through this narrow port; the EF implementation
// (schema "decisions") lives in Infrastructure and maps only its own tables (ADR-0001, docs/34 §12).
public interface IDecisionsDbContext
{
    DbSet<Decision> Decisions { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
