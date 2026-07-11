using System.Data.Common;
using Acmp.Modules.Meetings.Application;
using Acmp.Modules.Meetings.Application.Abstractions;
using Acmp.Modules.Meetings.Infrastructure.Directory;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Shared.Contracts.Meetings;
using Acmp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Meetings.Infrastructure;

// Single entry point the API host calls to wire the Meetings module.
public static class MeetingsInfrastructureExtensions
{
    public static IServiceCollection AddMeetingsModule(this IServiceCollection services, IConfiguration configuration)
    {
        // On the shared per-scope DbConnection (SharedKernel / ADR-0026 NFR-042) so a state change in this
        // module and its audit append (and any cross-module write in the same command) commit atomically.
        services.AddDbContext<MeetingsDbContext>((sp, options) =>
            options.UseSqlServer(sp.GetRequiredService<DbConnection>(), sql =>
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", MeetingsDbContext.Schema))
                .AddAcmpAuditInterceptors(sp));

        services.AddScoped<IMeetingsDbContext>(sp => sp.GetRequiredService<MeetingsDbContext>());
        services.AddScoped<IMeetingKeyGenerator, MeetingKeyGenerator>();

        // Cross-module seam for the Decisions Vote present-quorum gate (ADR-0001, docs/domain/entity-lifecycles.md §4).
        services.AddScoped<IMeetingQuorumSource, MeetingQuorumSource>();
        // Inbound Webex write seam (ADR-0021, P13): the Webex integration stores the correlation id + recording.
        services.AddScoped<IMeetingWebexWriter, MeetingWebexWriter>();
        // Default no-op meeting provisioner; the Webex adapter overrides it (when enabled) after this call.
        services.AddScoped<IWebexMeetingProvisioner, NullWebexMeetingProvisioner>();

        // Recording-upload constraints (FR-056): size cap + allowed video MIME types.
        services.Configure<MeetingRecordingOptions>(configuration.GetSection(MeetingRecordingOptions.SectionName));

        services.AddMeetingsApplication();
        return services;
    }
}
