using Acmp.Modules.Actions.Application;
using Acmp.Modules.Actions.Application.Abstractions;
using Acmp.Modules.Actions.Infrastructure.Directory;
using Acmp.Modules.Actions.Infrastructure.Persistence;
using Acmp.Shared.Contracts.Actions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Actions.Infrastructure;

// Single entry point the API host calls to wire the Actions module.
public static class ActionsInfrastructureExtensions
{
    public static IServiceCollection AddActionsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Acmp");
        services.AddDbContext<ActionsDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", ActionsDbContext.Schema)));

        services.AddScoped<IActionsDbContext>(sp => sp.GetRequiredService<ActionsDbContext>());
        services.AddScoped<IActionKeyGenerator, ActionKeyGenerator>();

        // Cross-module seam for the Decisions AC-029 downstream-link gate (ADR-0001, FR-067, OQ-045).
        services.AddScoped<IActionLinkDirectory, ActionLinkDirectory>();

        services.AddActionsApplication();
        return services;
    }
}
