using Acmp.Modules.Meetings.Application;
using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Infrastructure.Directory;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Shared.Contracts.Meetings;
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

        // Cross-module seam for the Decisions Vote present-quorum gate (ADR-0001, docs/domain/entity-lifecycles.md §4).
        services.AddScoped<IMeetingQuorumSource, MeetingQuorumSource>();

        services.AddMeetingsApplication();
        return services;
    }
}
