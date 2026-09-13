using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Notifications.Application;

public static class NotificationsApplicationExtensions
{
    public static readonly System.Reflection.Assembly Assembly = typeof(NotificationsApplicationExtensions).Assembly;

    // The feed and mark-read requests are guarded by current-user scoping alone; the preferences update
    // (FR-133 / AC-160) also carries field rules, so the module's validators are registered.
    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly);
        return services;
    }
}
