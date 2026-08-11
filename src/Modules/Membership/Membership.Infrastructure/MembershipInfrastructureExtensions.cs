using System.Data.Common;
using Acmp.Modules.Membership.Application;
using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Infrastructure.Authorization;
using Acmp.Modules.Membership.Infrastructure.Directory;
using Acmp.Modules.Membership.Infrastructure.Identity;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Shared.Authorization.Abac;
using Acmp.Shared.Contracts.Membership;
using Acmp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

        // ADR-0038 — the application's first outbound WRITE to the identity provider. Registered
        // ONLY when configured, so an environment without the service-account credential cannot
        // invite or assign roles rather than failing at the first click. The options validator runs
        // ValidateOnStart, so a half-configured environment stops the host at boot instead.
        services.AddOptions<KeycloakAdminOptions>()
            .Bind(configuration.GetSection(KeycloakAdminOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<KeycloakAdminOptions>, KeycloakAdminOptionsValidator>();

        var keycloakAdmin = configuration.GetSection(KeycloakAdminOptions.SectionName).Get<KeycloakAdminOptions>();
        if (keycloakAdmin?.Enabled == true)
        {
            services.AddHttpClient<IIdentityProvider, KeycloakAdminClient>(client =>
            {
                // Trailing slash matters: every request path is relative, so without it BaseAddress
                // resolution silently drops the last segment (e.g. the /kc prefix behind nginx).
                var baseUrl = keycloakAdmin.BaseUrl.EndsWith('/') ? keycloakAdmin.BaseUrl : keycloakAdmin.BaseUrl + "/";
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(15);
            });
        }

        services.AddMembershipApplication();
        return services;
    }
}
