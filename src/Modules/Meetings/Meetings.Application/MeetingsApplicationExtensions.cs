using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Acmp.Modules.Meetings.Application;

public static class MeetingsApplicationExtensions
{
    public static readonly System.Reflection.Assembly Assembly = typeof(MeetingsApplicationExtensions).Assembly;

    public static IServiceCollection AddMeetingsApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly);
        return services;
    }
}
