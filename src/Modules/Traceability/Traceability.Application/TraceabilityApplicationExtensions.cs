using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Traceability.Application;

public static class TraceabilityApplicationExtensions
{
    public static readonly System.Reflection.Assembly Assembly = typeof(TraceabilityApplicationExtensions).Assembly;

    public static IServiceCollection AddTraceabilityApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly);
        return services;
    }
}
