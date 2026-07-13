using System.Data.Common;
using Acmp.Modules.Research.Application;
using Acmp.Modules.Research.Application.Abstractions;
using Acmp.Modules.Research.Infrastructure.Persistence;
using Acmp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Research.Infrastructure;

// Single entry point the API host calls to wire the Research module (P15a).
public static class ResearchInfrastructureExtensions
{
    public static IServiceCollection AddResearchModule(this IServiceCollection services, IConfiguration configuration)
    {
        // On the shared per-scope DbConnection (SharedKernel / ADR-0026 NFR-042) so a state change in this
        // module and its audit append (and any cross-module write in the same command) commit atomically.
        // .AddAcmpAuditInterceptors(sp) attaches the before/after capture — without it INV-005 silently falls
        // back to lean audit rows.
        services.AddDbContext<ResearchDbContext>((sp, options) =>
            options.UseSqlServer(sp.GetRequiredService<DbConnection>(), sql =>
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", ResearchDbContext.Schema))
                .AddAcmpAuditInterceptors(sp));

        services.AddScoped<IResearchDbContext>(sp => sp.GetRequiredService<ResearchDbContext>());
        services.AddScoped<IResearchKeyGenerator, ResearchKeyGenerator>();

        // Cross-module read seam (P15c / W16): the Topics convert reads a mission/recommendation through this.
        services.AddScoped<Acmp.Shared.Contracts.Research.IResearchReader, Directory.ResearchReader>();

        services.AddResearchApplication();
        return services;
    }
}
