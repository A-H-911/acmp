using System;
using Acmp.Shared.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Acmp.Modules.Membership.Infrastructure.Persistence;

// Design-time only: lets "dotnet ef migrations add" build the context without running the API
// or connecting to a database. Never used at runtime.
public sealed class MembershipDbContextFactory : IDesignTimeDbContextFactory<MembershipDbContext>
{
    public MembershipDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MembershipDbContext>()
            .UseSqlServer("Server=localhost;Database=Acmp;TrustServerCertificate=True",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", MembershipDbContext.Schema))
            .Options;
        return new MembershipDbContext(options, new DesignTimeClock(), new DesignTimeUser());
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
        public IReadOnlyCollection<string> Roles => Array.Empty<string>();
        public bool IsInRole(string role) => false;
    }
}
