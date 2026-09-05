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
    private static ServiceProvider BuildProvider(ICurrentUser user, TokenProbe? probe = null)
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

        // Registered LAST, so it is the INNERMOST behavior and observes whatever the three above
        // actually forwarded. Only wired when a probe is supplied, so no other test is affected.
        if (probe is not null)
        {
            services.AddSingleton(probe);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TokenProbeBehavior<,>));
        }

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

    // ── DW-098 / WBS-28 — THE CALLER'S CancellationToken MUST SURVIVE EVERY BEHAVIOR ─────────────
    //
    // ⚠⚠ THIS TEST EXISTS BECAUSE THE FAILURE IT GUARDS AGAINST IS A PASS, NOT A FAILURE. MediatR
    // 12.5.0 gave RequestHandlerDelegate<T> a CancellationToken parameter WITH A DEFAULT VALUE. A
    // behavior that calls bare next() therefore still compiles, still passes every other test in
    // this suite, and silently forwards CancellationToken.None instead of the caller's token —
    // cancellation stops propagating and nothing anywhere reports it. Measured: `next()` compiles
    // under both 12.4.1 and 12.5.0, while `next(ct)` compiles only under 12.5.0 (CS1593 before).
    // No compilation check at any scope can see this; only an assertion on the forwarded token can.
    // LL-032: when the dangerous outcome is a PASS, the guard has to be explicit.
    //
    // ⭐ THIS GUARD WAS VERIFIED TO FAIL BEFORE THE FOUR BEHAVIORS WERE FIXED — it observed
    // CancellationToken.None while every other test in the suite stayed green. A guard that has
    // never been shown to fail proves nothing (LL-013, LL-041).
    [Fact(DisplayName = "Pipeline: the caller's CancellationToken is forwarded through every behavior")]
    public async Task Cancellation_token_is_forwarded_through_every_behavior()
    {
        var probe = new TokenProbe();
        await using var sp = BuildProvider(User(true, nameof(CommitteeRole.Administrator)), probe);
        var memberId = await SeedMemberAsync(sp);

        using var cts = new CancellationTokenSource();
        using var scope = sp.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new DeactivateMemberCommand(memberId), cts.Token);

        probe.Ran.Should().BeTrue("the probe behavior must actually execute, or a green result here means nothing");
        probe.Seen.Should().Be(cts.Token,
            "every behavior must forward the caller's token with next(ct); a bare next() silently substitutes CancellationToken.None");
    }

    // Records the token the innermost behavior was handed. One instance per test, supplied by the
    // test itself, so nothing is shared between tests (LL-032: a fixture shared across tests changes
    // meaning when somebody does ordinary work).
    private sealed class TokenProbe
    {
        public CancellationToken Seen { get; set; }

        public bool Ran { get; set; }
    }

    private sealed class TokenProbeBehavior<TRequest, TResponse>(TokenProbe probe)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
        {
            probe.Seen = ct;
            probe.Ran = true;
            return next(ct);
        }
    }
}
