using System.Data.Common;
using Acmp.Modules.Traceability.Application;
using Acmp.Modules.Traceability.Application.Abstractions;
using Acmp.Modules.Traceability.Infrastructure.Directory;
using Acmp.Modules.Traceability.Infrastructure.Persistence;
using Acmp.Shared.Contracts.Traceability;
using Acmp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Traceability.Infrastructure;

// Single entry point the API host calls to wire the Traceability module.
public static class TraceabilityInfrastructureExtensions
{
    public static IServiceCollection AddTraceabilityModule(this IServiceCollection services, IConfiguration configuration)
    {
        // On the shared per-scope DbConnection (SharedKernel / ADR-0026 NFR-042) so a state change in this
        // module and its audit append (and any cross-module write in the same command) commit atomically.
        services.AddDbContext<TraceabilityDbContext>((sp, options) =>
            options.UseSqlServer(sp.GetRequiredService<DbConnection>(), sql =>
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", TraceabilityDbContext.Schema))
                .AddAcmpAuditInterceptors(sp));

        services.AddScoped<ITraceabilityDbContext>(sp => sp.GetRequiredService<TraceabilityDbContext>());

        // Cross-module port powering the widened AC-029 decision-issue gate (Decisions consumes it).
        services.AddScoped<ITraceabilityLinks, TraceabilityLinks>();

        // Cross-module WRITE port: lets an authorized action record a system edge (P11e Decision→ADR).
        services.AddScoped<ITraceabilityWriter, TraceabilityWriter>();

        services.AddTraceabilityApplication();
        return services;
    }
}
