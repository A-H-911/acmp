using Acmp.Modules.Dependencies.Application.Features.CreateDependency;
using Acmp.Modules.Dependencies.Application.Features.GetDependenciesForArtifact;
using Acmp.Modules.Dependencies.Application.Features.GetDependenciesRegister;
using Acmp.Modules.Dependencies.Application.Features.GetDependencyByKey;
using Acmp.Modules.Dependencies.Application.Features.RemoveDependency;
using Acmp.Modules.Dependencies.Application.Features.ResolveDependency;
using Acmp.Modules.Dependencies.Domain;
using Acmp.Modules.Dependencies.Domain.Enums;
using Acmp.Modules.Dependencies.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Dependencies;

// Round-trips through the real DependenciesDbContext (InMemory): the Dependency edge mapping, the key
// generator, the create/resolve/remove flow with audit, the register filters/sort/paging, and the panel
// outbound/inbound split. Proven without a running SQL Server.
public class DependencyHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private static DependenciesDbContext Db(string name, ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<DependenciesDbContext>().UseInMemoryDatabase(name).Options, clock, user);

    private static DependenciesDbContext NewDb(ICurrentUser user, IClock clock) =>
        Db("deps-" + Guid.NewGuid(), user, clock);

    private static ICurrentUser User(string sub = "kc-sec", string name = "Sam Secretary")
    {
        var u = Substitute.For<ICurrentUser>();
        u.IsAuthenticated.Returns(true);
        u.UserId.Returns(sub);
        u.DisplayName.Returns(name);
        return u;
    }

    private static IClock Clock(DateTimeOffset now)
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(now);
        return c;
    }

    private static CreateDependencyCommand CreateCmd(
        DependencyEndpointType ft = DependencyEndpointType.Topic, Guid? fid = null, string fk = "TOP-2026-042", string ftitle = "Gateway",
        DependencyEndpointType tt = DependencyEndpointType.Action, Guid? tid = null, string tk = "ACT-2026-009", string ttitle = "Rotate keys",
        DependencyKind kind = DependencyKind.BlockedBy, string? note = null) =>
        new(ft, fid ?? Guid.NewGuid(), fk, ftitle, tt, tid ?? Guid.NewGuid(), tk, ttitle, kind, note);

    // ---- CreateDependency --------------------------------------------------------------------------------

    [Fact]
    public async Task Create_allocates_a_key_persists_the_edge_and_audits()
    {
        var name = "c-" + Guid.NewGuid();
        var audit = Substitute.For<IAuditSink>();
        string key;
        await using (var db = Db(name, User(), Clock(Now)))
            key = await new CreateDependencyHandler(db, new DependencyKeyGenerator(db), User(), Clock(Now), audit)
                .Handle(CreateCmd(note: "handle first"), default);

        key.Should().Be("DPN-2026-001");

        await using var read = Db(name, User(), Clock(Now));
        var edge = await read.Dependencies.SingleAsync();
        edge.Key.Should().Be("DPN-2026-001");
        edge.FromKey.Should().Be("TOP-2026-042");
        edge.ToKey.Should().Be("ACT-2026-009");
        edge.Kind.Should().Be(DependencyKind.BlockedBy);
        edge.Status.Should().Be(DependencyStatus.Open);
        edge.Note.Should().Be("handle first");
        edge.CreatedBy.Should().Be("kc-sec");
        await audit.Received(1).EmitEnrichedAsync("Dependency.Created", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_rejects_a_self_loop_at_the_domain_guard()
    {
        var id = Guid.NewGuid();
        await using var db = NewDb(User(), Clock(Now));
        var act = () => new CreateDependencyHandler(db, new DependencyKeyGenerator(db), User(), Clock(Now), Substitute.For<IAuditSink>())
            .Handle(CreateCmd(ft: DependencyEndpointType.Topic, fid: id, tt: DependencyEndpointType.Topic, tid: id), default);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*itself*");
    }

    [Theory]
    [InlineData("selfloop")]
    [InlineData("emptyFrom")]
    [InlineData("emptyTo")]
    [InlineData("emptyKey")]
    [InlineData("emptyTitle")]
    [InlineData("longNote")]
    [InlineData("badKind")]
    public void Validator_rejects_invalid_commands(string kind)
    {
        var id = Guid.NewGuid();
        var cmd = kind switch
        {
            "selfloop" => CreateCmd(ft: DependencyEndpointType.Topic, fid: id, tt: DependencyEndpointType.Topic, tid: id),
            "emptyFrom" => CreateCmd(fid: Guid.Empty),
            "emptyTo" => CreateCmd(tid: Guid.Empty),
            "emptyKey" => CreateCmd(fk: ""),
            "emptyTitle" => CreateCmd(ttitle: ""),
            "longNote" => CreateCmd(note: new string('x', 1001)),
            _ => CreateCmd(kind: (DependencyKind)0),
        };
        new CreateDependencyValidator().Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_accepts_a_valid_command() =>
        new CreateDependencyValidator().Validate(CreateCmd()).IsValid.Should().BeTrue();

    // ---- Resolve / Remove --------------------------------------------------------------------------------

    private static async Task<(string Name, Guid Id, string Key)> SeededAsync(DependencyKind kind = DependencyKind.BlockedBy)
    {
        var name = "flow-" + Guid.NewGuid();
        await using var db = Db(name, User(), Clock(Now));
        await new CreateDependencyHandler(db, new DependencyKeyGenerator(db), User(), Clock(Now), Substitute.For<IAuditSink>())
            .Handle(CreateCmd(kind: kind), default);
        var edge = await db.Dependencies.SingleAsync();
        return (name, edge.PublicId, edge.Key);
    }

    [Fact]
    public async Task Resolve_moves_open_to_resolved_and_audits()
    {
        var (name, id, _) = await SeededAsync();
        var audit = Substitute.For<IAuditSink>();
        await using (var db = Db(name, User(), Clock(Now)))
            await new ResolveDependencyHandler(db, User(), audit).Handle(new ResolveDependencyCommand(id), default);

        await using var read = Db(name, User(), Clock(Now));
        (await read.Dependencies.SingleAsync()).Status.Should().Be(DependencyStatus.Resolved);
        await audit.Received(1).EmitEnrichedAsync("Dependency.Resolved", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolve_unknown_throws_not_found()
    {
        await using var db = NewDb(User(), Clock(Now));
        var act = () => new ResolveDependencyHandler(db, User(), Substitute.For<IAuditSink>())
            .Handle(new ResolveDependencyCommand(Guid.NewGuid()), default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Resolve_a_non_open_edge_throws_conflict()
    {
        var (name, id, _) = await SeededAsync();
        await using (var db = Db(name, User(), Clock(Now)))
            await new ResolveDependencyHandler(db, User(), Substitute.For<IAuditSink>()).Handle(new ResolveDependencyCommand(id), default);

        await using var db2 = Db(name, User(), Clock(Now));
        var act = () => new ResolveDependencyHandler(db2, User(), Substitute.For<IAuditSink>()).Handle(new ResolveDependencyCommand(id), default);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Remove_moves_open_to_removed_and_audits()
    {
        var (name, id, _) = await SeededAsync();
        var audit = Substitute.For<IAuditSink>();
        await using (var db = Db(name, User(), Clock(Now)))
            await new RemoveDependencyHandler(db, User(), audit).Handle(new RemoveDependencyCommand(id), default);

        await using var read = Db(name, User(), Clock(Now));
        (await read.Dependencies.SingleAsync()).Status.Should().Be(DependencyStatus.Removed);
        await audit.Received(1).EmitEnrichedAsync("Dependency.Removed", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Remove_unknown_throws_not_found()
    {
        await using var db = NewDb(User(), Clock(Now));
        var act = () => new RemoveDependencyHandler(db, User(), Substitute.For<IAuditSink>())
            .Handle(new RemoveDependencyCommand(Guid.NewGuid()), default);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Remove_a_non_open_edge_throws_conflict()
    {
        var (name, id, _) = await SeededAsync();
        await using (var db = Db(name, User(), Clock(Now)))
            await new RemoveDependencyHandler(db, User(), Substitute.For<IAuditSink>()).Handle(new RemoveDependencyCommand(id), default);

        await using var db2 = Db(name, User(), Clock(Now));
        var act = () => new RemoveDependencyHandler(db2, User(), Substitute.For<IAuditSink>()).Handle(new RemoveDependencyCommand(id), default);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ---- GetDependencyByKey ------------------------------------------------------------------------------

    [Fact]
    public async Task GetByKey_returns_the_detail_with_derived_blocker_flag()
    {
        var (name, _, key) = await SeededAsync(DependencyKind.BlockedBy);
        await using var read = Db(name, User(), Clock(Now));
        var dto = await new GetDependencyByKeyHandler(read).Handle(new GetDependencyByKeyQuery(key), default);
        dto!.Key.Should().Be(key);
        dto.Kind.Should().Be("BlockedBy");
        dto.Status.Should().Be("Open");
        dto.IsBlocker.Should().BeTrue();          // BlockedBy + Open
    }

    [Fact]
    public async Task GetByKey_returns_null_for_an_unknown_key()
    {
        await using var db = NewDb(User(), Clock(Now));
        (await new GetDependencyByKeyHandler(db).Handle(new GetDependencyByKeyQuery("DPN-2026-999"), default)).Should().BeNull();
    }

    [Fact] // A non-blocker kind is not a blocker even while Open.
    public async Task GetByKey_isBlocker_false_for_dependsOn()
    {
        var (name, _, key) = await SeededAsync(DependencyKind.DependsOn);
        await using var read = Db(name, User(), Clock(Now));
        (await new GetDependencyByKeyHandler(read).Handle(new GetDependencyByKeyQuery(key), default))!.IsBlocker.Should().BeFalse();
    }

    // ---- Register ----------------------------------------------------------------------------------------

    private static Dependency Seed(string key, DependencyKind kind, DependencyStatus status = DependencyStatus.Open)
    {
        var d = Dependency.Create(key, DependencyEndpointType.Topic, Guid.NewGuid(), "TOP-" + key, "T",
            DependencyEndpointType.Action, Guid.NewGuid(), "ACT-" + key, "A", kind, null);
        if (status == DependencyStatus.Resolved) d.Resolve();
        if (status == DependencyStatus.Removed) d.Remove();
        return d;
    }

    private static async Task<string> RegisterFixtureAsync()
    {
        var name = "reg-" + Guid.NewGuid();
        await using var db = Db(name, User(), Clock(Now));
        db.Dependencies.Add(Seed("DPN-2026-001", DependencyKind.BlockedBy));                       // blocker, open
        db.Dependencies.Add(Seed("DPN-2026-002", DependencyKind.DependsOn));                       // open, not blocker
        db.Dependencies.Add(Seed("DPN-2026-003", DependencyKind.Blocks, DependencyStatus.Resolved)); // resolved
        db.Dependencies.Add(Seed("DPN-2026-004", DependencyKind.RelatesTo, DependencyStatus.Removed)); // removed
        await db.SaveChangesAsync();
        return name;
    }

    [Fact]
    public async Task Register_excludes_removed_by_default_and_filters_by_kind_status_and_blocked()
    {
        var name = await RegisterFixtureAsync();
        await using var read = Db(name, User(), Clock(Now));
        var h = new GetDependenciesRegisterHandler(read);

        (await h.Handle(new GetDependenciesRegisterQuery(), default)).Total.Should().Be(3);              // Removed excluded
        (await h.Handle(new GetDependenciesRegisterQuery(Status: DependencyStatus.Removed), default)).Total.Should().Be(1); // explicit
        (await h.Handle(new GetDependenciesRegisterQuery(Status: DependencyStatus.Resolved), default)).Total.Should().Be(1);
        (await h.Handle(new GetDependenciesRegisterQuery(Kind: DependencyKind.BlockedBy), default)).Total.Should().Be(1);
        (await h.Handle(new GetDependenciesRegisterQuery(BlockedOnly: true), default)).Total.Should().Be(1); // only the open BlockedBy
    }

    [Fact]
    public async Task Register_sorts_and_pages()
    {
        var name = await RegisterFixtureAsync();
        await using var read = Db(name, User(), Clock(Now));
        var h = new GetDependenciesRegisterHandler(read);

        var byKeyDesc = await h.Handle(new GetDependenciesRegisterQuery(SortBy: "key", SortDir: "desc"), default);
        byKeyDesc.Items[0].Key.Should().Be("DPN-2026-003");   // highest key among the 3 non-removed

        var byStatus = await h.Handle(new GetDependenciesRegisterQuery(SortBy: "status", SortDir: "asc"), default);
        byStatus.Total.Should().Be(3);

        var paged = await h.Handle(new GetDependenciesRegisterQuery(SortBy: "key", SortDir: "asc", Page: 1, PageSize: 2), default);
        paged.Items.Should().HaveCount(2);
        paged.Total.Should().Be(3);
        paged.Items[0].Key.Should().Be("DPN-2026-001");
    }

    // ---- GetDependenciesForArtifact (panel) --------------------------------------------------------------

    [Fact]
    public async Task Panel_splits_outbound_and_inbound_and_excludes_removed()
    {
        var name = "panel-" + Guid.NewGuid();
        var topicId = Guid.NewGuid();
        await using (var db = Db(name, User(), Clock(Now)))
        {
            // outbound: topic --BlockedBy--> action
            db.Dependencies.Add(Dependency.Create("DPN-2026-001", DependencyEndpointType.Topic, topicId, "TOP-1", "T",
                DependencyEndpointType.Action, Guid.NewGuid(), "ACT-1", "A", DependencyKind.BlockedBy, null));
            // inbound: decision --DependsOn--> topic
            db.Dependencies.Add(Dependency.Create("DPN-2026-002", DependencyEndpointType.Decision, Guid.NewGuid(), "DECN-1", "D",
                DependencyEndpointType.Topic, topicId, "TOP-1", "T", DependencyKind.DependsOn, null));
            // removed outbound — must not surface
            var removed = Dependency.Create("DPN-2026-003", DependencyEndpointType.Topic, topicId, "TOP-1", "T",
                DependencyEndpointType.Action, Guid.NewGuid(), "ACT-2", "A", DependencyKind.RelatesTo, null);
            removed.Remove();
            db.Dependencies.Add(removed);
            await db.SaveChangesAsync();
        }

        await using var read = Db(name, User(), Clock(Now));
        var panel = await new GetDependenciesForArtifactHandler(read)
            .Handle(new GetDependenciesForArtifactQuery(DependencyEndpointType.Topic, topicId), default);

        panel.Outbound.Should().ContainSingle();
        panel.Outbound[0].OtherType.Should().Be("Action");
        panel.Outbound[0].OtherKey.Should().Be("ACT-1");
        panel.Outbound[0].IsBlocker.Should().BeTrue();

        panel.Inbound.Should().ContainSingle();
        panel.Inbound[0].OtherType.Should().Be("Decision");
        panel.Inbound[0].OtherKey.Should().Be("DECN-1");
        panel.Inbound[0].IsBlocker.Should().BeFalse();
    }
}
