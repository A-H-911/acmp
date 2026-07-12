using System.Data.Common;
using Acmp.Modules.Membership.Application;
using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Infrastructure.Authorization;
using Acmp.Modules.Membership.Infrastructure.Directory;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Shared.Authorization.Abac;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Membership.Infrastructure;

// Single entry point the API host calls to wire the Membership module.
public static class MembershipInfrastructureExtensions
{
    public static IServiceCollection AddMembershipModule(this IServiceCollection services, IConfiguration configuration)
    {
        // On the shared per-scope DbConnection (SharedKernel / ADR-0026 NFR-042) so a state change in this
        // module and its audit append (and any cross-module write in the same command) commit atomically.
        services.AddDbContext<MembershipDbContext>((sp, options) =>
            options.UseSqlServer(sp.GetRequiredService<DbConnection>(), sql =>
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", MembershipDbContext.Schema))
                .AddAcmpAuditInterceptors(sp));

        services.AddScoped<IMembershipDbContext>(sp => sp.GetRequiredService<MembershipDbContext>());

        // ABAC ports the shared-kernel authorization handlers depend on (docs/domain/permission-role-matrix.md §D/§E).
        services.AddScoped<IUserStreamProvider, UserStreamProvider>();
        services.AddScoped<ITopicCapabilityResolver, TopicCapabilityResolver>();
        services.AddScoped<ITopicCapabilityWriter, TopicCapabilityWriter>();
        services.AddScoped<IDelegationResolver, DelegationResolver>();

        // Cross-module roster lookup consumed by the Meetings notification fan-out (ADR-0001).
        services.AddScoped<ICommitteeDirectory, CommitteeDirectory>();

        services.AddMembershipApplication();
        return services;
    }
}
