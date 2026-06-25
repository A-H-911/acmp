using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Behaviors;
using Acmp.Shared.Infrastructure.FileStorage;
using Acmp.Shared.Infrastructure.Identity;
using Acmp.Shared.Infrastructure.Time;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;

namespace Acmp.Shared;

// Composition root for the shared kernel: cross-cutting services + MediatR pipeline behaviors.
public static class SharedKernelExtensions
{
    public static IServiceCollection AddSharedKernel(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUserService>();
        services.AddSingleton<IClock, SystemClock>();

        // Behaviors wrap the handler outer-to-inner in registration order:
        // logging (outermost) -> authorization -> validation (closest to the handler).
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // MinIO object storage (ADR-0014). Building the client does not open a connection.
        var minio = configuration.GetSection("Minio");
        var endpoint = minio["Endpoint"] ?? "localhost:9000";
        var accessKey = minio["AccessKey"] ?? "minioadmin";
        var secretKey = minio["SecretKey"] ?? "minioadmin";
        var secure = bool.TryParse(minio["Secure"], out var s) && s;
        services.AddSingleton<IMinioClient>(_ => new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(secure)
            .Build());
        services.AddScoped<IFileStore, MinioFileStore>();

        return services;
    }
}
