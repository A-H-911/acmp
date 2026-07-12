using System.Data.Common;
using Acmp.Modules.Governance.Application;
using Acmp.Modules.Governance.Application.Abstractions;
using Acmp.Modules.Governance.Infrastructure.Persistence;
using Acmp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Governance.Infrastructure;

// Single entry point the API host calls to wire the Governance module (ADRs + Invariants).
public static class GovernanceInfrastructureExtensions
{
    public static IServiceCollection AddGovernanceModule(this IServiceCollection services, IConfiguration configuration)
    {
        // On the shared per-scope DbConnection (SharedKernel / ADR-0026 NFR-042) so a state change in this
        // module and its audit append (and any cross-module write in the same command) commit atomically.
        services.AddDbContext<GovernanceDbContext>((sp, options) =>
            options.UseSqlServer(sp.GetRequiredService<DbConnection>(), sql =>
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", GovernanceDbContext.Schema))
                .AddAcmpAuditInterceptors(sp));

        services.AddScoped<IGovernanceDbContext>(sp => sp.GetRequiredService<GovernanceDbContext>());
        services.AddScoped<IAdrKeyGenerator, AdrKeyGenerator>();
        services.AddScoped<IInvariantKeyGenerator, InvariantKeyGenerator>();

        services.AddGovernanceApplication();
        return services;
    }
}
