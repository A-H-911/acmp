using Acmp.Modules.Meetings.Application;
using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Meetings.Infrastructure;

// Single entry point the API host calls to wire the Meetings module.
public static class MeetingsInfrastructureExtensions
{
    public static IServiceCollection AddMeetingsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Acmp");
        services.AddDbContext<MeetingsDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", MeetingsDbContext.Schema)));

        services.AddScoped<IMeetingsDbContext>(sp => sp.GetRequiredService<MeetingsDbContext>());
        services.AddScoped<IMeetingKeyGenerator, MeetingKeyGenerator>();

        services.AddMeetingsApplication();
        return services;
    }
}
