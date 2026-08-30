using Acmp.Modules.Topics.Application.Abstractions;
using Acmp.Modules.Topics.Application.Features.GetBacklog;
using Acmp.Modules.Topics.Application.Features.GetTopicDetail;
using Acmp.Modules.Topics.Application.Internal;
using Acmp.Modules.Topics.Domain;
using Acmp.Modules.Topics.Domain.Enums;
using Acmp.Modules.Topics.Infrastructure.Persistence;
using Acmp.Shared.Application.Abstractions;
using Acmp.Shared.Authorization;
using Acmp.Shared.Authorization.Abac;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Acmp.Application.Tests.Topics;

/// <summary>
/// FR-163 / C-AUTHZ-04 (DEC-063) — the READ-side narrowing, proven by forcing the refusal.
///
/// ⚠ THIS SUITE EXISTS BECAUSE THE REST OF THE TEST BASE CANNOT SEE THIS CONTROL. Every other topic
/// suite stubs ITopicVisibility permissively so it keeps measuring backlog filtering, detail mapping
/// or search-engine behaviour — which means the whole suite stays green whether the predicate narrows
/// anything or not. Each case below is written so it FAILS if the predicate is removed.
/// </summary>
public class TopicVisibilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Granted = Guid.NewGuid();

    private static TopicsDbContext NewDb(ICurrentUser user, IClock clock) =>
        new(new DbContextOptionsBuilder<TopicsDbContext>().UseInMemoryDatabase("vis-" + Guid.NewGuid()).Options,
            clock, user);

    private static ICurrentUser User(params string[] roles)
    {
        var u = Substitute.For<ICurrentUser>();
        u.IsAuthenticated.Returns(true);
        u.UserId.Returns("kc-member");
        u.DisplayName.Returns("Mo M.");
        u.Roles.Returns(roles);
        u.IsInRole(Arg.Any<string>()).Returns(c => roles.Contains(c.Arg<string>()));
        return u;
    }

    private static IClock Clock()
    {
        var c = Substitute.For<IClock>();
        c.UtcNow.Returns(Now);
        return c;
    }

    private static ITopicCapabilityResolver Capabilities(params Guid[] grantedTopicIds)
    {
        var r = Substitute.For<ITopicCapabilityResolver>();
        r.GetGrantedTopicIdsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(grantedTopicIds);
        return r;
    }

    private static Topic Restricted(string key)
    {
        var t = Open(key);
        t.Restrict("kc-sec", "Sara S.", Now);
        return t;
    }

    private static Topic Open(string key) => Topic.Draft(
        key, "Adopt Keycloak", "Consolidate IAM.", "Fragmented auth is risky.",
        TopicType.ArchitectureDecision, TopicUrgency.Normal, TopicSource.SecurityFinding,
        "kc-omar", "Omar H.", new[] { "identity" }, Array.Empty<string>(), Array.Empty<string>());

    private static async Task<TopicsDbContext> Seeded(ICurrentUser user, IClock clock, params Topic[] topics)
    {
        var db = NewDb(user, clock);
        foreach (var t in topics) { t.Submit(Now); db.Topics.Add(t); }
        await db.SaveChangesAsync();
        return db;
    }

    // ---- the resolver ----

    [Theory]
    [InlineData(AcmpRoles.Chairman)]
    [InlineData(AcmpRoles.Secretary)]
    [InlineData(AcmpRoles.Auditor)]
    public async Task A_committee_wide_reader_sees_everything(string role)
    {
        var scope = await new TopicVisibility(User(role), Capabilities()).ResolveAsync();

        scope.SeesAllRestricted.Should().BeTrue();
        scope.CanSee(Restricted("TOP-2026-001")).Should().BeTrue();
    }

    [Theory]
    [InlineData(AcmpRoles.Member)]
    [InlineData(AcmpRoles.Reviewer)]
    [InlineData(AcmpRoles.Submitter)]
    [InlineData(AcmpRoles.Guest)]
    [InlineData(AcmpRoles.Administrator)]
    public async Task Every_other_role_including_Administrator_is_narrowed(string role)
    {
        var scope = await new TopicVisibility(User(role), Capabilities()).ResolveAsync();

        // ⚠ Administrator is in this list DELIBERATELY. It is not named in C-AUTHZ-04, and
        // "confidentiality narrows, never widens" makes adding it the one drift this control must not
        // take — an operational role is not a governance reader.
        scope.SeesAllRestricted.Should().BeFalse();
        scope.CanSee(Restricted("TOP-2026-001")).Should().BeFalse();
    }

    [Fact]
    public async Task An_unrestricted_topic_is_visible_to_a_narrowed_principal()
    {
        var scope = await new TopicVisibility(User(AcmpRoles.Member), Capabilities()).ResolveAsync();

        // Confidentiality NARROWS, never widens: with nothing classified the control must be inert.
        scope.CanSee(Open("TOP-2026-002")).Should().BeTrue();
    }

    [Fact]
    public async Task A_grantee_sees_the_restricted_topic_they_hold_a_grant_on_and_no_other()
    {
        var mine = Restricted("TOP-2026-003");
        var theirs = Restricted("TOP-2026-004");
        var scope = await new TopicVisibility(User(AcmpRoles.Member), Capabilities(mine.PublicId)).ResolveAsync();

        scope.CanSee(mine).Should().BeTrue("an explicit capability grant is the Owner/Assignee/Presenter leg");
        scope.CanSee(theirs).Should().BeFalse("a grant on one topic must not leak another");
    }

    [Fact]
    public async Task A_principal_with_no_subject_claim_is_narrowed_rather_than_opened()
    {
        var anon = Substitute.For<ICurrentUser>();
        anon.IsInRole(Arg.Any<string>()).Returns(false);
        anon.UserId.Returns((string?)null);

        var scope = await new TopicVisibility(anon, Capabilities()).ResolveAsync();

        // Fail closed. A control whose whole job is to withhold must not open up on a missing claim.
        scope.SeesAllRestricted.Should().BeFalse();
        scope.CanSee(Restricted("TOP-2026-005")).Should().BeFalse();
    }

    // ---- the query predicate, through the real handlers ----

    [Fact]
    public async Task The_backlog_hides_a_restricted_topic_from_a_non_grantee()
    {
        var (user, clock) = (User(AcmpRoles.Member), Clock());
        await using var db = await Seeded(user, clock, Open("TOP-2026-010"), Restricted("TOP-2026-011"));
        var visibility = new TopicVisibility(user, Capabilities());

        var page = await new GetBacklogHandler(db, clock, visibility)
            .Handle(new GetBacklogQuery(), CancellationToken.None);

        page.Items.Select(i => i.Key).Should().ContainSingle().Which.Should().Be("TOP-2026-010");
        // ⚠ THE TOTAL MATTERS AS MUCH AS THE ROWS. If the predicate ran after paging, the count would
        // still say 2 and the UI would render "1 of 2" — announcing the existence of something the
        // caller may not see, which is the fact the classification protects.
        page.Total.Should().Be(1);
    }

    [Fact]
    public async Task The_backlog_shows_a_restricted_topic_to_a_grantee()
    {
        var (user, clock) = (User(AcmpRoles.Member), Clock());
        var restricted = Restricted("TOP-2026-012");
        await using var db = await Seeded(user, clock, restricted);
        var visibility = new TopicVisibility(user, Capabilities(restricted.PublicId));

        var page = await new GetBacklogHandler(db, clock, visibility)
            .Handle(new GetBacklogQuery(), CancellationToken.None);

        page.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task The_backlog_shows_a_restricted_topic_to_the_secretary()
    {
        var (user, clock) = (User(AcmpRoles.Secretary), Clock());
        await using var db = await Seeded(user, clock, Restricted("TOP-2026-013"));
        var visibility = new TopicVisibility(user, Capabilities());

        var page = await new GetBacklogHandler(db, clock, visibility)
            .Handle(new GetBacklogQuery(), CancellationToken.None);

        page.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Detail_by_key_is_refused_as_NOT_FOUND_for_a_non_grantee()
    {
        var (user, clock) = (User(AcmpRoles.Member), Clock());
        await using var db = await Seeded(user, clock, Restricted("TOP-2026-020"));
        var visibility = new TopicVisibility(user, Capabilities());

        var detail = await new GetTopicDetailHandler(db, clock, visibility, Substitute.For<IAnomalyDetector>())
            .Handle(new GetTopicDetailQuery("TOP-2026-020"), CancellationToken.None);

        // null renders as 404. Asserting null rather than an exception IS the assertion: a 403 would
        // confirm the key names a real topic, and the direct-by-key route is the IDOR path this closes.
        detail.Should().BeNull();
    }

    [Fact]
    public async Task Detail_by_key_is_served_to_a_grantee_with_its_materials()
    {
        var (user, clock) = (User(AcmpRoles.Member), Clock());
        var restricted = Restricted("TOP-2026-021");
        // A restricted topic's ATTACHMENTS are the most sensitive thing it carries, so the grantee
        // path is asserted to return them rather than merely to return a non-null envelope. This also
        // covers the attachment projection, which no other detail case exercises.
        restricted.AddAttachment("finding.pdf", "application/pdf", 2048, "topics/TOP-2026-021/finding.pdf",
            "kc-omar", "Omar H.", Now);
        await using var db = await Seeded(user, clock, restricted);
        var visibility = new TopicVisibility(user, Capabilities(restricted.PublicId));

        var detail = await new GetTopicDetailHandler(db, clock, visibility, Substitute.For<IAnomalyDetector>())
            .Handle(new GetTopicDetailQuery("TOP-2026-021"), CancellationToken.None);

        detail.Should().NotBeNull();
        detail!.Restricted.Should().BeTrue("the badge needs the flag on the wire");
        detail.Attachments.Should().ContainSingle().Which.FileName.Should().Be("finding.pdf");
    }

    [Fact]
    public void The_in_memory_and_query_predicates_agree_on_every_case()
    {
        // ⚠ THE TWO HALVES MUST NOT DRIFT. CanSee decides one loaded topic (detail); VisibleTo runs in
        // SQL over a page (lists, search). If they ever disagree, a topic is filtered out of the list
        // but readable by key, or the reverse — the shape that makes a control look broken rather than
        // strict. Asserted against ONE table of cases rather than each against its own.
        var open = Open("TOP-2026-030");
        var mine = Restricted("TOP-2026-031");
        var theirs = Restricted("TOP-2026-032");
        var all = new[] { open, mine, theirs };

        foreach (var scope in new[]
                 {
                     new TopicVisibilityScope(true, Array.Empty<Guid>()),
                     new TopicVisibilityScope(false, Array.Empty<Guid>()),
                     new TopicVisibilityScope(false, new[] { mine.PublicId }),
                 })
        {
            var byQuery = all.AsQueryable().VisibleTo(scope).Select(t => t.Key).OrderBy(k => k).ToArray();
            var byPredicate = all.Where(scope.CanSee).Select(t => t.Key).OrderBy(k => k).ToArray();
            byQuery.Should().Equal(byPredicate, "the SQL predicate and the in-memory check are one control");
        }
    }

    // ---- the cross-module egress port (SL-030 / AC-114) ----------------------------------------------

    private static TopicConfidentialityReader Port(TopicsDbContext db, ICurrentUser user, params Guid[] granted) =>
        new(db, new TopicVisibility(user, Capabilities(granted)));

    [Theory]
    [InlineData(AcmpRoles.Chairman)]
    [InlineData(AcmpRoles.Secretary)]
    [InlineData(AcmpRoles.Auditor)]
    public async Task The_port_hides_nothing_from_a_committee_wide_reader(string role)
    {
        var user = User(role);
        await using var db = await Seeded(user, Clock(), Restricted("TOP-2026-040"), Open("TOP-2026-041"));

        (await Port(db, user).GetHiddenTopicIdsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task The_port_hides_the_restricted_topics_and_only_those()
    {
        var user = User(AcmpRoles.Member);
        var secret = Restricted("TOP-2026-040");
        var open = Open("TOP-2026-041");
        await using var db = await Seeded(user, Clock(), secret, open);

        var hidden = await Port(db, user).GetHiddenTopicIdsAsync();

        hidden.Should().ContainSingle().Which.Should().Be(secret.PublicId);
        // An unrestricted topic must never enter the hidden set: the redaction consumers apply it to
        // EVERY snapshot they hold, so a false positive here blanks the whole committee's agendas.
        hidden.Should().NotContain(open.PublicId);
    }

    [Fact]
    public async Task A_grantee_is_not_hidden_from_their_own_restricted_topic()
    {
        var user = User(AcmpRoles.Member);
        var mine = Restricted("TOP-2026-040");
        var theirs = Restricted("TOP-2026-041");
        await using var db = await Seeded(user, Clock(), mine, theirs);

        var hidden = await Port(db, user, mine.PublicId).GetHiddenTopicIdsAsync();

        hidden.Should().ContainSingle().Which.Should().Be(theirs.PublicId);
    }

    [Fact]
    public async Task The_hidden_set_is_exactly_the_complement_of_VisibleTo()
    {
        // ⚠ THIS IS THE PROPERTY THE PORT EXISTS TO PRESERVE. The hidden set is DERIVED from VisibleTo by
        // subtraction rather than written as a fourth predicate, so that the rule keeps one expression.
        // If someone ever rewrites the reader as a hand-rolled Where, this is what fails.
        var user = User(AcmpRoles.Member);
        var mine = Restricted("TOP-2026-040");
        var theirs = Restricted("TOP-2026-041");
        var open = Open("TOP-2026-042");
        await using var db = await Seeded(user, Clock(), mine, theirs, open);

        var scope = await new TopicVisibility(user, Capabilities(mine.PublicId)).ResolveAsync();
        var visible = db.Topics.VisibleTo(scope).Select(t => t.PublicId).ToHashSet();
        var everything = db.Topics.Select(t => t.PublicId).ToHashSet();

        var hidden = await Port(db, user, mine.PublicId).GetHiddenTopicIdsAsync();

        hidden.Should().BeEquivalentTo(everything.Except(visible));
    }

    [Fact]
    public async Task The_port_returns_empty_when_no_topic_is_restricted_at_all()
    {
        var user = User(AcmpRoles.Member);
        await using var db = await Seeded(user, Clock(), Open("TOP-2026-040"), Open("TOP-2026-041"));

        (await Port(db, user).GetHiddenTopicIdsAsync()).Should().BeEmpty();
    }
}
