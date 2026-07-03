using Acmp.Modules.Dependencies.Application;
using Acmp.Modules.Dependencies.Application.Abstractions;
using Acmp.Modules.Dependencies.Infrastructure.Directory;
using Acmp.Modules.Dependencies.Infrastructure.Persistence;
using Acmp.Shared.Contracts.Dependencies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Dependencies.Infrastructure;

// Single entry point the API host calls to wire the Dependencies module.
public static class DependenciesInfrastructureExtensions
{
    public static IServiceCollection AddDependenciesModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Acmp");
        services.AddDbContext<DependenciesDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", DependenciesDbContext.Schema)));

        services.AddScoped<IDependenciesDbContext>(sp => sp.GetRequiredService<DependenciesDbContext>());
        services.AddScoped<IDependencyKeyGenerator, DependencyKeyGenerator>();

        // Cross-module port the Traceability impact-graph composer reads through (P10f, ADR-0001).
        services.AddScoped<IDependencyArtifactReader, DependencyArtifactReader>();

        services.AddDependenciesApplication();
        return services;
    }
}
