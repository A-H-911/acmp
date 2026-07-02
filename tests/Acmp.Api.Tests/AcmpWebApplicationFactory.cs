using Acmp.Modules.Actions.Infrastructure.Persistence;
using Acmp.Modules.Decisions.Infrastructure.Persistence;
using Acmp.Modules.Meetings.Infrastructure.Persistence;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Modules.Notifications.Infrastructure.Persistence;
using Acmp.Modules.Risks.Infrastructure.Persistence;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Infrastructure.Audit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Acmp.Api.Tests;

// Boots the real API host with two test swaps: the Membership DbContext points at a private
// in-memory store, and authentication uses the header-driven TestAuthHandler instead of Keycloak.
// Everything else (the MediatR pipeline, policy authorization, Problem Details) runs unchanged.
public sealed class AcmpWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "acmp-it-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<MembershipDbContext>>();
            services.RemoveAll<MembershipDbContext>();
            services.AddDbContext<MembershipDbContext>(o => o.UseInMemoryDatabase(_dbName));

            services.RemoveAll<DbContextOptions<TopicsDbContext>>();
            services.RemoveAll<TopicsDbContext>();
            services.AddDbContext<TopicsDbContext>(o => o.UseInMemoryDatabase(_dbName + "-topics"));

            services.RemoveAll<DbContextOptions<MeetingsDbContext>>();
            services.RemoveAll<MeetingsDbContext>();
            services.AddDbContext<MeetingsDbContext>(o => o.UseInMemoryDatabase(_dbName + "-meetings"));

            services.RemoveAll<DbContextOptions<DecisionsDbContext>>();
            services.RemoveAll<DecisionsDbContext>();
            services.AddDbContext<DecisionsDbContext>(o => o.UseInMemoryDatabase(_dbName + "-decisions"));

            services.RemoveAll<DbContextOptions<ActionsDbContext>>();
            services.RemoveAll<ActionsDbContext>();
            services.AddDbContext<ActionsDbContext>(o => o.UseInMemoryDatabase(_dbName + "-actions"));

            services.RemoveAll<DbContextOptions<RisksDbContext>>();
            services.RemoveAll<RisksDbContext>();
            services.AddDbContext<RisksDbContext>(o => o.UseInMemoryDatabase(_dbName + "-risks"));

            services.RemoveAll<DbContextOptions<NotificationsDbContext>>();
            services.RemoveAll<NotificationsDbContext>();
            services.AddDbContext<NotificationsDbContext>(o => o.UseInMemoryDatabase(_dbName + "-notifications"));

            services.RemoveAll<DbContextOptions<AuditDbContext>>();
            services.RemoveAll<AuditDbContext>();
            services.AddDbContext<AuditDbContext>(o => o.UseInMemoryDatabase(_dbName + "-audit"));

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    public async Task SeedMembersAsync(params (string Sub, string Name, CommitteeRole Role)[] members)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MembershipDbContext>();
        foreach (var (sub, name, role) in members)
            db.Members.Add(CommitteeMember.Provision(sub, name, $"{sub}@acmp.gov", role, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }
}
