using Acmp.Modules.Notifications.Application;
using Acmp.Modules.Notifications.Application.Abstractions;
using Acmp.Modules.Notifications.Application.Channels;
using Acmp.Modules.Notifications.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Notifications.Infrastructure;

// Single entry point the API host calls to wire the Notifications module. Registers the in-app channel
// as the one INotificationChannel implementation (ADR-0005, AC-053) — any module that publishes a
// NotificationMessage resolves this synchronously.
public static class NotificationsInfrastructureExtensions
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Acmp");
        services.AddDbContext<NotificationsDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", NotificationsDbContext.Schema)));

        services.AddScoped<INotificationsDbContext>(sp => sp.GetRequiredService<NotificationsDbContext>());
        services.AddScoped<INotificationChannel, InAppNotificationChannel>();

        services.AddNotificationsApplication();
        return services;
    }
}
