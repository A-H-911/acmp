using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Decisions.Application;

public static class DecisionsApplicationExtensions
{
    public static readonly System.Reflection.Assembly Assembly = typeof(DecisionsApplicationExtensions).Assembly;

    public static IServiceCollection AddDecisionsApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly);
        return services;
    }
}
