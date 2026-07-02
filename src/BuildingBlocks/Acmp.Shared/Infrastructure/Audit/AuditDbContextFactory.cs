using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Acmp.Shared.Infrastructure.Audit;

// Design-time only: lets "dotnet ef migrations add" build AuditDbContext without the API or a database.
// Excluded from coverage (coverlet.runsettings *DbContextFactory rule).
public sealed class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseSqlServer("Server=localhost;Database=Acmp;TrustServerCertificate=True",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", AuditDbContext.Schema))
            .Options;
        return new AuditDbContext(options);
    }
}
