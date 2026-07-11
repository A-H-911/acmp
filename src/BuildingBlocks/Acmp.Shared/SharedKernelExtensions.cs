using System.Data.Common;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Behaviors;
using Acmp.Shared.Infrastructure.Audit;
using Acmp.Shared.Infrastructure.FileStorage;
using Acmp.Shared.Infrastructure.Identity;
using Acmp.Shared.Infrastructure.Persistence;
using Acmp.Shared.Infrastructure.Time;
using MediatR;
using Microsoft.Data.SqlClient;
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

        // NFR-042 (ADR-0026): ONE shared DbConnection per request scope. Every module DbContext + AuditDbContext
        // is wired onto this single connection so a command's state change and its audit append commit on ONE
        // local transaction (no MSDTC escalation). Constructing a SqlConnection opens nothing; the API test host
        // swaps every context to EF-InMemory, which never resolves this and never touches SQL.
        // MARS is enabled because contexts now SHARE one physical connection: EF buffers query results by default
        // (readers close on await), so today's handlers are fine, but MARS removes the "open DataReader" failure
        // class should a future handler stream a query on one context while touching another. It is compatible
        // with the single ambient local transaction (one tx, multiple readers).
        services.AddScoped<DbConnection>(_ => new SqlConnection(
            new SqlConnectionStringBuilder(configuration.GetConnectionString("Acmp"))
            {
                MultipleActiveResultSets = true,
            }.ConnectionString));
        services.AddScoped<AmbientTransaction>();

        // ADR-0026: request-scoped before/after capture + same-transaction atomicity interceptors. Registered as
        // concrete scoped types and attached EXPLICITLY per context via .AddAcmpAuditInterceptors(sp) — EF's DI
        // auto-apply of IInterceptor does not fire for these contexts (proven by AuditAtomicityTests).
        // AuditCaptureInterceptor snapshots before/after; AmbientTransactionStarter begins the tx on the first
        // module write; AmbientTransactionInterceptor enlists every subsequent command (reads included) so
        // nothing runs unenlisted on the shared connection.
        services.AddScoped<AuditChangeBuffer>();
        services.AddScoped<AuditCaptureInterceptor>();
        services.AddScoped<AmbientTransactionStarter>();
        services.AddScoped<AmbientTransactionInterceptor>();

        // BL-066 (ADR-0009): the durable, immutable, hash-chained AuditEvent store (schema "audit") behind
        // the IAuditSink seam. It also mirrors to Serilog->Seq, so the interim SerilogAuditSink is retired.
        // On the shared connection (above) so its append joins the command transaction.
        services.AddDbContext<AuditDbContext>((sp, options) =>
            options.UseSqlServer(sp.GetRequiredService<DbConnection>(), sql =>
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", AuditDbContext.Schema))
                .AddAcmpAuditInterceptors(sp));
        services.AddScoped<IAuditSink, SqlAuditSink>();

        // Behaviors wrap the handler outer-to-inner in registration order: logging (outermost) -> authorization
        // -> validation -> transaction (innermost). Transaction is innermost so auth/validation DENIALS (which
        // emit-then-throw) run OUTSIDE the command transaction and autocommit their audit rows (ADR-0026).
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

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
