using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Research.Application;

public static class ResearchApplicationExtensions
{
    public static readonly System.Reflection.Assembly Assembly = typeof(ResearchApplicationExtensions).Assembly;

    public static IServiceCollection AddResearchApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly);
        return services;
    }
}
