using Acmp.Modules.Governance.Application.Features.PromoteDecisionToAdr;
using Acmp.Modules.Governance.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Contracts.Decisions;
using Acmp.Shared.Contracts.Traceability;
using Acmp.Shared.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Governance;

// FR-068 Decision→ADR promotion over the real GovernanceDbContext (InMemory): the pre-fill mapping, the born-
// Draft + SourceDecisionId link, the RecordedAs traceability edge (via the ITraceabilityWriter seam), the audit,
// and the eligibility/idempotency guards (not-found 404, not-Issued 409, already-promoted 409).
public class PromoteDecisionToAdrTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid DecId = Guid.NewGuid();
    private static readonly LocalizedString Title = LocalizedString.Create("Adopt Keycloak", "اعتماد كيكلوك");
    private static readonly LocalizedString Statement = LocalizedString.Create("Adopt Keycloak, realm per stream.", "اعتماد كيكلوك، نطاق لكل مسار.");
    private static readonly LocalizedString Rationale = LocalizedString.Create("Fragmented auth across streams.", "مصادقة مجزأة عبر المسارات.");
    private static readonly LocalizedString Alternatives = LocalizedString.Create("In-house IdP; per-stream stacks.", "موفّر داخلي؛ حزم لكل مسار.");

    private static GovernanceDbContext Db(ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<GovernanceDbContext>().UseInMemoryDatabase("gov-promote-" + Guid.NewGuid()).Options, clock, user);

    private static ICurrentUser User(string sub = "kc-chair", string name = "Chair")
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

    private sealed class RecordingAudit : IAuditSink
    {
        public List<(string Event, string? Sub, object? Data)> Calls { get; } = new();
        public Task EmitAsync(string e, string? s, object? d = null, CancellationToken ct = default) { Calls.Add((e, s, d)); return Task.CompletedTask; }
    }

    private sealed class FakeDecisionReader : IDecisionReader
    {
        private readonly DecisionForPromotion? _d;
        public FakeDecisionReader(DecisionForPromotion? d) => _d = d;
        public Task<DecisionForPromotion?> GetForPromotionAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_d);
    }

    private sealed class RecordingTraceWriter : ITraceabilityWriter
    {
        public List<(string SrcType, Guid SrcId, string TgtType, Guid TgtId, string Rel)> Edges { get; } = new();
        public Task RecordEdgeAsync(string sourceType, Guid sourceId, string sourceKey, string sourceTitle,
            string targetType, Guid targetId, string targetKey, string targetTitle, string relTypeName, CancellationToken ct = default)
        {
            Edges.Add((sourceType, sourceId, targetType, targetId, relTypeName));
            return Task.CompletedTask;
        }
    }

    private static DecisionForPromotion Decision(string status = "Issued") =>
        new(DecId, "DECN-2026-008", status, Title with { }, Statement with { }, Rationale with { }, Alternatives with { });

    private static PromoteDecisionToAdrHandler Handler(GovernanceDbContext db, ICurrentUser user, IClock clock,
        IAuditSink audit, IDecisionReader reader, ITraceabilityWriter trace) =>
        new(db, new AdrKeyGenerator(db), user, clock, audit, reader, trace);

    [Fact]
    public async Task Promote_issued_decision_creates_draft_adr_prefilled_linked_and_audited()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var audit = new RecordingAudit();
        var trace = new RecordingTraceWriter();

        var summary = await Handler(db, user, clock, audit, new FakeDecisionReader(Decision()), trace)
            .Handle(new PromoteDecisionToAdrCommand(DecId), CancellationToken.None);

        summary.Key.Should().Be("ADR-2026-001");
        summary.Status.Should().Be("Draft");

        // Pre-fill mapping + SourceDecisionId link.
        var adr = await db.Adrs.SingleAsync();
        adr.SourceDecisionId.Should().Be(DecId);
        adr.Title.En.Should().Be(Title.En);
        adr.Context.En.Should().Be(Rationale.En);        // Context ← Rationale
        adr.DecisionText.En.Should().Be(Statement.En);   // Decision ← Statement
        adr.DecisionDrivers!.En.Should().Be(Alternatives.En); // Drivers ← Alternatives

        // RecordedAs edge Decision → ADR.
        trace.Edges.Should().ContainSingle();
        trace.Edges[0].Should().Be(("Decision", DecId, "Adr", adr.PublicId, "RecordedAs"));

        audit.Calls.Should().Contain(c => c.Event == "Governance.AdrPromotedFromDecision");
    }

    [Fact]
    public async Task Promote_missing_decision_throws_not_found()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);

        var act = () => Handler(db, user, clock, new RecordingAudit(), new FakeDecisionReader(null), new RecordingTraceWriter())
            .Handle(new PromoteDecisionToAdrCommand(DecId), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Promote_non_issued_decision_is_rejected()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);

        var act = () => Handler(db, user, clock, new RecordingAudit(), new FakeDecisionReader(Decision("Draft")), new RecordingTraceWriter())
            .Handle(new PromoteDecisionToAdrCommand(DecId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*issued*");
        (await db.Adrs.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Promote_already_promoted_decision_is_blocked_and_names_the_existing_adr()
    {
        var user = User(); var clock = Clock(Now);
        await using var db = Db(user, clock);
        var reader = new FakeDecisionReader(Decision());

        var first = await Handler(db, user, clock, new RecordingAudit(), reader, new RecordingTraceWriter())
            .Handle(new PromoteDecisionToAdrCommand(DecId), CancellationToken.None);

        var act = () => Handler(db, user, clock, new RecordingAudit(), reader, new RecordingTraceWriter())
            .Handle(new PromoteDecisionToAdrCommand(DecId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage($"*{first.Key}*");
        (await db.Adrs.CountAsync()).Should().Be(1); // no duplicate ADR
    }

    [Fact]
    public void Validator_requires_a_decision_id()
    {
        new PromoteDecisionToAdrValidator().Validate(new PromoteDecisionToAdrCommand(Guid.Empty)).IsValid.Should().BeFalse();
        new PromoteDecisionToAdrValidator().Validate(new PromoteDecisionToAdrCommand(DecId)).IsValid.Should().BeTrue();
    }
}
