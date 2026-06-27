using Acmp.Modules.Meetings.Domain;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Application.Abstractions;

// The Application layer talks to persistence through this narrow port; the EF implementation
// (schema "meetings") lives in Infrastructure and maps only its own tables (ADR-0001, docs/34 §12).
public interface IMeetingsDbContext
{
    DbSet<Meeting> Meetings { get; }
    DbSet<Agenda> Agendas { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
