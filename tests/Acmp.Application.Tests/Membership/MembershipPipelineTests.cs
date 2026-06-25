using Acmp.Modules.Membership.Application;
using Acmp.Modules.Membership.Application.Abstractions;
using Acmp.Modules.Membership.Application.Features.InviteMember;
using Acmp.Modules.Membership.Domain.Enums;
using Acmp.Modules.Membership.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Application.Behaviors;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Acmp.Application.Tests.Membership;

// Drives InviteMemberCommand through the REAL MediatR pipeline (logging -> authorization -> validation
// -> handler), wired exactly as SharedKernelExtensions does. The handler-only tests instantiate the
// handler directly and bypass the behaviors + validator; these prove they actually run. docs/31 §2.2.
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

        services.AddScoped<MembershipDbContext>(_ => new MembershipDbContext(
            new DbContextOptionsBuilder<MembershipDbContext>()
                .UseInMemoryDatabase("pipeline-" + Guid.NewGuid()).Options,
            clock, user));
        services.AddScoped<IMembershipDbContext>(sp => sp.GetRequiredService<MembershipDbContext>());

        // Same behavior order as SharedKernelExtensions: logging -> authorization -> validation.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        services.AddMembershipApplication(); // module's real validator registration
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

    private static InviteMemberCommand ValidInvite() =>
        new("kc-1", "Valid Member", "valid@example.com", CommitteeRole.Member);

    [Fact(DisplayName = "Pipeline: Administrator + valid command -> validator + authz pass, member persisted")]
    public async Task Valid_command_with_allowed_role_passes_full_pipeline()
    {
        await using var sp = BuildProvider(User(true, nameof(CommitteeRole.Administrator)));
        using var scope = sp.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var response = await sender.Send(ValidInvite());

        response.Id.Should().BeGreaterThan(0);
        var db = scope.ServiceProvider.GetRequiredService<IMembershipDbContext>();
        (await db.Members.CountAsync()).Should().Be(1);
    }

    [Fact(DisplayName = "Pipeline: invalid command -> ValidationBehavior throws, handler never runs")]
    public async Task Invalid_command_is_rejected_by_validation_behavior()
    {
        await using var sp = BuildProvider(User(true, nameof(CommitteeRole.Administrator)));
        using var scope = sp.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var act = () => sender.Send(new InviteMemberCommand("", "", "not-an-email", CommitteeRole.Member));

        await act.Should().ThrowAsync<ValidationException>();
        var db = scope.ServiceProvider.GetRequiredService<IMembershipDbContext>();
        (await db.Members.CountAsync()).Should().Be(0);
    }

    [Fact(DisplayName = "Pipeline: unauthenticated -> AuthorizationBehavior blocks before the handler")]
    public async Task Unauthenticated_request_is_blocked()
    {
        await using var sp = BuildProvider(User(false));
        using var scope = sp.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var act = () => sender.Send(ValidInvite());

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact(DisplayName = "Pipeline: authenticated but wrong role -> AuthorizationBehavior blocks")]
    public async Task Authenticated_without_required_role_is_blocked()
    {
        await using var sp = BuildProvider(User(true, nameof(CommitteeRole.Member)));
        using var scope = sp.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var act = () => sender.Send(ValidInvite());

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
