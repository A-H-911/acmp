using Acmp.Modules.Topics.Application;
using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Infrastructure.Persistence;
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

        services.Configure<TopicAttachmentOptions>(configuration.GetSection(TopicAttachmentOptions.SectionName));

        services.AddTopicsApplication();
        return services;
    }
}
