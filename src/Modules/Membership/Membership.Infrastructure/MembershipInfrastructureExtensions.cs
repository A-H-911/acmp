using Acmp.Modules.Membership.Application;
using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Membership.Infrastructure;

// Single entry point the API host calls to wire the Membership module.
public static class MembershipInfrastructureExtensions
{
    public static IServiceCollection AddMembershipModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Acmp");
        services.AddDbContext<MembershipDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", MembershipDbContext.Schema)));

        services.AddScoped<IMembershipDbContext>(sp => sp.GetRequiredService<MembershipDbContext>());
        services.AddMembershipApplication();
        return services;
    }
}
