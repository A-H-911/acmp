using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Knowledge.Application;

public static class KnowledgeApplicationExtensions
{
    public static readonly System.Reflection.Assembly Assembly = typeof(KnowledgeApplicationExtensions).Assembly;

    public static IServiceCollection AddKnowledgeApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly);
        return services;
    }
}
