using Acmp.Modules.Actions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Actions.Application.Abstractions;

// The Application layer talks to persistence through this narrow port; the EF implementation
// (schema "actions") lives in Infrastructure and maps only its own tables (ADR-0001, docs/34 §12).
public interface IActionsDbContext
{
    DbSet<ActionItem> Actions { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
