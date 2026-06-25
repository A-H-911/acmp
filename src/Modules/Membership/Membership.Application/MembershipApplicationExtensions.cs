using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Membership.Application;

public static class MembershipApplicationExtensions
{
    public static readonly System.Reflection.Assembly Assembly = typeof(MembershipApplicationExtensions).Assembly;

    public static IServiceCollection AddMembershipApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly);
        return services;
    }
}
