using Acmp.Shared.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Acmp.Modules.Governance.Infrastructure.Persistence;

// Design-time only: lets "dotnet ef migrations add" build the context without the API or a database.
public sealed class GovernanceDbContextFactory : IDesignTimeDbContextFactory<GovernanceDbContext>
{
    public GovernanceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseSqlServer("Server=localhost;Database=Acmp;TrustServerCertificate=True",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", GovernanceDbContext.Schema))
            .Options;
        return new GovernanceDbContext(options, new DesignTimeClock(), new DesignTimeUser());
    }

    private sealed class DesignTimeClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private sealed class DesignTimeUser : ICurrentUser
    {
        public bool IsAuthenticated => false;
        public string? UserId => null;
        public string? UserName => null;
        public string? Email => null;
        public string? DisplayName => null;
        public IReadOnlyCollection<string> Roles => Array.Empty<string>();
        public bool IsInRole(string role) => false;
    }
}
