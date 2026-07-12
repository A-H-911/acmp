using System.Data.Common;
using Acmp.Modules.Risks.Application;
using Acmp.Modules.Risks.Application.Abstractions;
using Acmp.Modules.Risks.Infrastructure.Persistence;
using Acmp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Risks.Infrastructure;

// Single entry point the API host calls to wire the Risks module.
public static class RisksInfrastructureExtensions
{
    public static IServiceCollection AddRisksModule(this IServiceCollection services, IConfiguration configuration)
    {
        // On the shared per-scope DbConnection (SharedKernel / ADR-0026 NFR-042) so a state change in this
        // module and its audit append (and any cross-module write in the same command) commit atomically.
        services.AddDbContext<RisksDbContext>((sp, options) =>
            options.UseSqlServer(sp.GetRequiredService<DbConnection>(), sql =>
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", RisksDbContext.Schema))
                .AddAcmpAuditInterceptors(sp));

        services.AddScoped<IRisksDbContext>(sp => sp.GetRequiredService<RisksDbContext>());
        services.AddScoped<IRiskKeyGenerator, RiskKeyGenerator>();

        services.AddRisksApplication();
        return services;
    }
}
