using System.Data.Common;
using Acmp.Modules.Topics.Application;
using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Contracts.Topics;
using Acmp.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Topics.Infrastructure;

// Single entry point the API host calls to wire the Topics module.
public static class TopicsInfrastructureExtensions
{
    public static IServiceCollection AddTopicsModule(this IServiceCollection services, IConfiguration configuration)
    {
        // On the shared per-scope DbConnection (SharedKernel / ADR-0026 NFR-042) so a state change in this
        // module and its audit append (and any cross-module write in the same command) commit atomically.
        services.AddDbContext<TopicsDbContext>((sp, options) =>
            options.UseSqlServer(sp.GetRequiredService<DbConnection>(), sql =>
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", TopicsDbContext.Schema))
                .AddAcmpAuditInterceptors(sp));

        services.AddScoped<ITopicsDbContext>(sp => sp.GetRequiredService<TopicsDbContext>());
        services.AddScoped<ITopicKeyGenerator, TopicKeyGenerator>();

        // Cross-module seam consumed by the Meetings module (ADR-0001): advance a topic's lifecycle on
        // agenda publish (Prepared→Scheduled) and meeting start (Scheduled→InCommittee).
        services.AddScoped<ITopicScheduler, TopicScheduler>();

        // Cross-module seam consumed by the Decisions module (ADR-0001): advance a topic to Decided when a
        // decision is issued (InCommittee→Decided); idempotent.
        services.AddScoped<ITopicDecisionRecorder, TopicDecisionRecorder>();

        // Cross-module read seam consumed by the Traceability impact graph (ADR-0001, P10f / FR-095): a
        // topic's affected-stream codes, for Topic↔Topic cross-stream classification.
        services.AddScoped<ITopicStreamReader, TopicStreamReader>();

        services.Configure<TopicAttachmentOptions>(configuration.GetSection(TopicAttachmentOptions.SectionName));

        services.AddTopicsApplication();
        return services;
    }
}
