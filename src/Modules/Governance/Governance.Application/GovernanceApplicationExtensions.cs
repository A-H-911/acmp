using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Governance.Application;

public static class GovernanceApplicationExtensions
{
    public static readonly System.Reflection.Assembly Assembly = typeof(GovernanceApplicationExtensions).Assembly;

    public static IServiceCollection AddGovernanceApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly);
        return services;
    }
}
