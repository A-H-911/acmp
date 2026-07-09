using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Acmp.Modules.Integrations.Webex.Oauth;

// Design-time only: lets "dotnet ef migrations add" build the context without the API or a database.
public sealed class WebexDbContextFactory : IDesignTimeDbContextFactory<WebexDbContext>
{
    public WebexDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<WebexDbContext>()
            .UseSqlServer("Server=localhost;Database=Acmp;TrustServerCertificate=True",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", WebexDbContext.Schema))
            .Options;
        return new WebexDbContext(options);
    }
}
