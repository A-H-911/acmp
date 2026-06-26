using Acmp.Modules.Topics.Domain;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Topics.Application.Abstractions;

// The Application layer talks to persistence through this narrow port; the EF implementation
// (schema "topics") lives in Infrastructure and maps only its own tables (ADR-0001, docs/34 §12).
public interface ITopicsDbContext
{
    DbSet<Topic> Topics { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
