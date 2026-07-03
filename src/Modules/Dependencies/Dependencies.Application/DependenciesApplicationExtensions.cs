using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Dependencies.Application;

public static class DependenciesApplicationExtensions
{
    public static readonly System.Reflection.Assembly Assembly = typeof(DependenciesApplicationExtensions).Assembly;

    public static IServiceCollection AddDependenciesApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly);
        return services;
    }
}
