using Acmp.Modules.Membership.Application;
using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Application.Features.DeactivateMember;
using Acmp.Modules.Membership.Domain;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Behaviors;
using Acmp.Shared.Application.Exceptions;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Acmp.Application.Tests.Membership;

// Drives an authorized command (DeactivateMember, Administrator-only) through the REAL MediatR
// pipeline (logging -> authorization -> validation -> handler), wired exactly as
// SharedKernelExtensions does. Proves the behaviors + validator actually run and that the
// authorization behavior applies the corrected 401-vs-403 split. docs/31 §2.2.
public class MembershipPipelineTests
{
    private static ServiceProvider BuildProvider(ICurrentUser user)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        services.AddSingleton<IClock>(clock);
        services.AddSingleton(user);
        services.AddSingleton(Substitute.For<IAuditSink>());

        // One stable in-memory database name per provider so every scope shares the same store.
        var dbName = "pipeline-" + Guid.NewGuid();
        services.AddScoped<MembershipDbContext>(_ => new MembershipDbContext(
            new DbContextOptionsBuilder<MembershipDbContext>().UseInMemoryDatabase(dbName).Options,
            clock, user));
        services.AddScoped<IMembershipDbContext>(sp => sp.GetRequiredService<MembershipDbContext>());

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddMembershipApplication();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(MembershipApplicationExtensions.Assembly));

        return services.BuildServiceProvider();
    }

    private static ICurrentUser User(bool authenticated, params string[] roles)
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(authenticated);
        user.UserId.Returns(authenticated ? "tester" : null);
        user.Roles.Returns(roles);
        user.IsInRole(Arg.Any<string>()).Returns(ci => roles.Contains((string)ci[0]));
        return user;
    }

    private static async Task<Guid> SeedMemberAsync(ServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MembershipDbContext>();
        var member = CommitteeMember.Provision("kc-seed", "Seed", "seed@x.com", CommitteeRole.Member, DateTimeOffset.UtcNow);
        db.Members.Add(member);
        await db.SaveChangesAsync();
        return member.PublicId;
    }

    [Fact(DisplayName = "Pipeline: Administrator + valid command -> validator + authz pass, member deactivated")]
    public async Task Valid_command_with_allowed_role_passes_full_pipeline()
    {
        await using var sp = BuildProvider(User(true, nameof(CommitteeRole.Administrator)));
        var memberId = await SeedMemberAsync(sp);

        using var scope = sp.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new DeactivateMemberCommand(memberId));

        var db = scope.ServiceProvider.GetRequiredService<IMembershipDbContext>();
        (await db.Members.SingleAsync()).IsActive.Should().BeFalse();
    }

    [Fact(DisplayName = "Pipeline: invalid command -> ValidationBehavior throws, handler never runs")]
    public async Task Invalid_command_is_rejected_by_validation_behavior()
    {
        await using var sp = BuildProvider(User(true, nameof(CommitteeRole.Administrator)));
        using var scope = sp.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var act = () => sender.Send(new DeactivateMemberCommand(Guid.Empty));

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact(DisplayName = "Pipeline: unauthenticated -> 401 (UnauthorizedAccessException)")]
    public async Task Unauthenticated_request_is_blocked_with_401()
    {
        await using var sp = BuildProvider(User(false));
        using var scope = sp.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var act = () => sender.Send(new DeactivateMemberCommand(Guid.NewGuid()));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact(DisplayName = "Pipeline: authenticated wrong role -> 403 (ForbiddenAccessException), not 401")]
    public async Task Authenticated_without_required_role_is_forbidden()
    {
        await using var sp = BuildProvider(User(true, nameof(CommitteeRole.Member)));
        using var scope = sp.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var act = () => sender.Send(new DeactivateMemberCommand(Guid.NewGuid()));

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }
}
