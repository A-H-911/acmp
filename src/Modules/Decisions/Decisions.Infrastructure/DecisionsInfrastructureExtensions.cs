using Acmp.Modules.Decisions.Application;
using Acmp.Modules.Decisions.Application.Abstractions;
using Acmp.Modules.Decisions.Infrastructure.Directory;
using Acmp.Modules.Decisions.Infrastructure.Persistence;
using Acmp.Shared.Contracts.Decisions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Decisions.Infrastructure;

// Single entry point the API host calls to wire the Decisions module.
public static class DecisionsInfrastructureExtensions
{
    public static IServiceCollection AddDecisionsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Acmp");
        services.AddDbContext<DecisionsDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", DecisionsDbContext.Schema)));

        services.AddScoped<IDecisionsDbContext>(sp => sp.GetRequiredService<DecisionsDbContext>());
        services.AddScoped<IDecisionKeyGenerator, DecisionKeyGenerator>();

        // Cross-module read seam powering the FR-068 Decision→ADR promotion (Governance consumes it).
        services.AddScoped<IDecisionReader, DecisionReader>();

        services.AddDecisionsApplication();
        return services;
    }
}
