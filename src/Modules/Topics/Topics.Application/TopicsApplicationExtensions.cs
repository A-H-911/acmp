using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Topics.Application;

public static class TopicsApplicationExtensions
{
    public static readonly System.Reflection.Assembly Assembly = typeof(TopicsApplicationExtensions).Assembly;

    public static IServiceCollection AddTopicsApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly);
        return services;
    }
}
