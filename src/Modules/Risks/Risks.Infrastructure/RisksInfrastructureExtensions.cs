using Acmp.Modules.Risks.Application;
using Acmp.Modules.Risks.Application.Abstractions;
using Acmp.Modules.Risks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Risks.Infrastructure;

// Single entry point the API host calls to wire the Risks module.
public static class RisksInfrastructureExtensions
{
    public static IServiceCollection AddRisksModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Acmp");
        services.AddDbContext<RisksDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", RisksDbContext.Schema)));

        services.AddScoped<IRisksDbContext>(sp => sp.GetRequiredService<RisksDbContext>());
        services.AddScoped<IRiskKeyGenerator, RiskKeyGenerator>();

        services.AddRisksApplication();
        return services;
    }
}
