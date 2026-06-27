using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Domain;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Modules.Meetings.Infrastructure.Persistence;

// Maps ONLY the meetings schema (docs/34 §12: no cross-module tables). References to other modules
// (chair/presenter/attendee = CommitteeMember.PublicId; agenda item = Topic.PublicId) are by value,
// never by FK navigation (ADR-0001). Agenda references its Meeting by id, not an EF navigation.
public sealed class MeetingsDbContext : ModuleDbContext, IMeetingsDbContext
{
    public const string Schema = "meetings";

    public MeetingsDbContext(DbContextOptions<MeetingsDbContext> options, IClock clock, ICurrentUser currentUser)
        : base(options, clock, currentUser)
    {
    }

    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<Agenda> Agendas => Set<Agenda>();
    internal DbSet<MeetingKeyCounter> KeyCounters => Set<MeetingKeyCounter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MeetingsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
