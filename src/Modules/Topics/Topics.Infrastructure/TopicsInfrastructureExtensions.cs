using Acmp.Modules.Topics.Application;
using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Contracts.Topics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Topics.Infrastructure;

// Single entry point the API host calls to wire the Topics module.
public static class TopicsInfrastructureExtensions
{
    public static IServiceCollection AddTopicsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Acmp");
        services.AddDbContext<TopicsDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", TopicsDbContext.Schema)));

        services.AddScoped<ITopicsDbContext>(sp => sp.GetRequiredService<TopicsDbContext>());
        services.AddScoped<ITopicKeyGenerator, TopicKeyGenerator>();

        // Cross-module seam consumed by the Meetings module (ADR-0001): advance a topic's lifecycle on
        // agenda publish (Prepared→Scheduled) and meeting start (Scheduled→InCommittee).
        services.AddScoped<ITopicScheduler, TopicScheduler>();

        // Cross-module seam consumed by the Decisions module (ADR-0001): advance a topic to Decided when a
        // decision is issued (InCommittee→Decided); idempotent.
        services.AddScoped<ITopicDecisionRecorder, TopicDecisionRecorder>();

        services.Configure<TopicAttachmentOptions>(configuration.GetSection(TopicAttachmentOptions.SectionName));

        services.AddTopicsApplication();
        return services;
    }
}
