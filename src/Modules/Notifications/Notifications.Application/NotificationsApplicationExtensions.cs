using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Notifications.Application;

public static class NotificationsApplicationExtensions
{
    public static readonly System.Reflection.Assembly Assembly = typeof(NotificationsApplicationExtensions).Assembly;

    // No FluentValidation validators here — the two requests are guarded by current-user scoping, not
    // field rules. The handle exists for symmetry + to expose Assembly for the host's MediatR scan.
    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services) => services;
}
