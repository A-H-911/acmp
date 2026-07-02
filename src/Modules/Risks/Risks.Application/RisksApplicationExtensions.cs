using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Risks.Application;

public static class RisksApplicationExtensions
{
    public static readonly System.Reflection.Assembly Assembly = typeof(RisksApplicationExtensions).Assembly;

    public static IServiceCollection AddRisksApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly);
        return services;
    }
}
