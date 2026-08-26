using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Acmp.Shared.Infrastructure.Configuration;

// Design-time only: lets "dotnet ef migrations add" build ConfigurationDbContext without the API or a
// database. Excluded from coverage (coverlet.runsettings *DbContextFactory rule).
public sealed class ConfigurationDbContextFactory : IDesignTimeDbContextFactory<ConfigurationDbContext>
{
    public ConfigurationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ConfigurationDbContext>()
            .UseSqlServer("Server=localhost;Database=Acmp;TrustServerCertificate=True",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", ConfigurationDbContext.Schema))
            .Options;
        return new ConfigurationDbContext(options);
    }
}
