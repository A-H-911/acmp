using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Behaviors;
using Acmp.Shared.Infrastructure.Audit;
using Acmp.Shared.Infrastructure.FileStorage;
using Acmp.Shared.Infrastructure.Identity;
using Acmp.Shared.Infrastructure.Time;
using MediatR;
using Microsoft.EntityFrameworkCore;
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
        services.AddScoped<IResourceAuthorizer, ResourceAuthorizer>();
        services.AddSingleton<IClock, SystemClock>();

        // BL-066 (ADR-0009): the durable, immutable, hash-chained AuditEvent store (schema "audit") behind
        // the IAuditSink seam. It also mirrors to Serilog->Seq, so the interim SerilogAuditSink is retired.
        // Call sites are unchanged. The Api.Tests factory swaps this context to EF-InMemory like the modules.
        services.AddDbContext<AuditDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Acmp"), sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", AuditDbContext.Schema)));
        services.AddScoped<IAuditSink, SqlAuditSink>();

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

        // Presign client: uses the public endpoint (browser-reachable via nginx) when configured, so presigned
        // GET URLs resolve + validate from the browser (ADR-0014); else the internal client (local dev). The
        // explicit region avoids a region-discovery round-trip at presign time.
        var publicEndpoint = minio["PublicEndpoint"];
        var publicSecure = bool.TryParse(minio["PublicSecure"], out var ps) && ps;
        services.AddSingleton(sp => string.IsNullOrWhiteSpace(publicEndpoint)
            ? new MinioPresigner(sp.GetRequiredService<IMinioClient>())
            : new MinioPresigner(new MinioClient()
                .WithEndpoint(publicEndpoint)
                .WithCredentials(accessKey, secretKey)
                .WithSSL(publicSecure)
                .WithRegion("us-east-1")
                .Build()));
        services.AddScoped<IFileStore, MinioFileStore>();

        return services;
    }
}
