using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Acmp.Shared.Infrastructure.Persistence;

// Base DbContext for every module. Stamps AuditableEntity fields on save from IClock + ICurrentUser.
// Each module derives one of these and maps ONLY its own schema tables (docs/34 section 12).
public abstract class ModuleDbContext : DbContext
{
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;

    protected ModuleDbContext(DbContextOptions options, IClock clock, ICurrentUser currentUser)
        : base(options)
    {
        _clock = clock;
        _currentUser = currentUser;
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        StampAudit();
        return base.SaveChangesAsync(ct);
    }

    private void StampAudit()
    {
        var now = _clock.UtcNow;
        var actor = _currentUser.UserId ?? "system";
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = actor;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = actor;
            }
        }
    }
}
