using System.Data.Common;
using Acmp.Modules.Notifications.Application;
using Acmp.Modules.Notifications.Application.Abstractions;
using Acmp.Modules.Notifications.Application.Channels;
using Acmp.Modules.Notifications.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Infrastructure.Persistence;
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
        // On the shared per-scope DbConnection (SharedKernel / ADR-0026 NFR-042) so a state change in this
        // module and its audit append (and any cross-module write in the same command) commit atomically.
        services.AddDbContext<NotificationsDbContext>((sp, options) =>
            options.UseSqlServer(sp.GetRequiredService<DbConnection>(), sql =>
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", NotificationsDbContext.Schema))
                .AddAcmpAuditInterceptors(sp));

        services.AddScoped<INotificationsDbContext>(sp => sp.GetRequiredService<NotificationsDbContext>());
        // The dispatcher is the single INotificationChannel; it fans out to every registered sink. The
        // in-app sink is always present; the Webex adapter (P13) registers a second sink in its own module.
        services.AddScoped<INotificationSink, InAppNotificationChannel>();
        services.AddScoped<INotificationChannel, NotificationDispatcher>();

        services.AddNotificationsApplication();
        return services;
    }
}
