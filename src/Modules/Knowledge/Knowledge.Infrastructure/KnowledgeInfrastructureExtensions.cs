using System.Data.Common;
using Acmp.Modules.Knowledge.Application;
using Acmp.Modules.Knowledge.Application.Abstractions;
using Acmp.Modules.Knowledge.Infrastructure.Persistence;
using Acmp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Knowledge.Infrastructure;

// Single entry point the API host calls to wire the Knowledge module (P15d).
public static class KnowledgeInfrastructureExtensions
{
    public static IServiceCollection AddKnowledgeModule(this IServiceCollection services, IConfiguration configuration)
    {
        // On the shared per-scope DbConnection (SharedKernel / ADR-0026 NFR-042) so a state change in this
        // module and its audit append (and any cross-module write in the same command) commit atomically.
        // .AddAcmpAuditInterceptors(sp) attaches the before/after capture — without it INV-005 silently falls
        // back to lean audit rows.
        services.AddDbContext<KnowledgeDbContext>((sp, options) =>
            options.UseSqlServer(sp.GetRequiredService<DbConnection>(), sql =>
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", KnowledgeDbContext.Schema))
                .AddAcmpAuditInterceptors(sp));

        services.AddScoped<IKnowledgeDbContext>(sp => sp.GetRequiredService<KnowledgeDbContext>());
        services.AddScoped<IKnowledgeKeyGenerator, KnowledgeKeyGenerator>();

        services.AddKnowledgeApplication();
        // Global-search provider (P15f, FR-118/143): the host fans out over every registered ISearchProvider.
        services.AddScoped<Acmp.Shared.Contracts.Search.ISearchProvider, Acmp.Modules.Knowledge.Infrastructure.Search.DocumentSearchProvider>();

        return services;
    }
}
