using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Actions.Application;

public static class ActionsApplicationExtensions
{
    public static readonly System.Reflection.Assembly Assembly = typeof(ActionsApplicationExtensions).Assembly;

    public static IServiceCollection AddActionsApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly);
        return services;
    }
}
