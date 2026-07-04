using Acmp.Modules.Governance.Application;
using Acmp.Modules.Governance.Application.Abstractions;
using Acmp.Modules.Governance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Governance.Infrastructure;

// Single entry point the API host calls to wire the Governance module (ADRs + Invariants).
public static class GovernanceInfrastructureExtensions
{
    public static IServiceCollection AddGovernanceModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Acmp");
        services.AddDbContext<GovernanceDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", GovernanceDbContext.Schema)));

        services.AddScoped<IGovernanceDbContext>(sp => sp.GetRequiredService<GovernanceDbContext>());
        services.AddScoped<IAdrKeyGenerator, AdrKeyGenerator>();
        services.AddScoped<IInvariantKeyGenerator, InvariantKeyGenerator>();

        services.AddGovernanceApplication();
        return services;
    }
}
